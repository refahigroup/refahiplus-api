using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Refahi.Modules.PaymentGateway.Application.Contracts.Configuration;
using Refahi.Modules.PaymentGateway.Application.Contracts.Exceptions;
using Refahi.Modules.PaymentGateway.Application.Contracts.Features.ProcessCallback;
using Refahi.Modules.PaymentGateway.Application.Contracts.Providers;
using Refahi.Modules.PaymentGateway.Application.Contracts.Repositories;
using Refahi.Modules.PaymentGateway.Domain.Aggregates;
using Refahi.Modules.PaymentGateway.Domain.Enums;
using Refahi.Modules.Wallets.Application.Contracts;
using Refahi.Modules.Wallets.Application.Contracts.Exceptions;
using Refahi.Modules.Wallets.Application.Contracts.Features.TopUp;

namespace Refahi.Modules.PaymentGateway.Application.Features.ProcessCallback;

public sealed class ProcessCallbackCommandHandler
    : IRequestHandler<ProcessCallbackCommand, ProcessCallbackResponse>
{
    private readonly IPaymentGatewaySessionRepository _sessions;
    private readonly IPaymentGatewayProviderFactory _providers;
    private readonly IMediator _mediator;
    private readonly FundingSafetyOptions _options;
    private readonly ILogger<ProcessCallbackCommandHandler> _logger;

    public ProcessCallbackCommandHandler(
        IPaymentGatewaySessionRepository sessions,
        IPaymentGatewayProviderFactory providers,
        IMediator mediator,
        IOptions<FundingSafetyOptions> options,
        ILogger<ProcessCallbackCommandHandler> logger)
    {
        _sessions = sessions;
        _providers = providers;
        _mediator = mediator;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<ProcessCallbackResponse> Handle(
        ProcessCallbackCommand command,
        CancellationToken ct)
    {
        if (!Guid.TryParse(command.ResNum, out var sessionId))
            return new ProcessCallbackResponse(Guid.Empty, false,
                "/charge/wallet/topup/result/invalid");

        var session = await _sessions.GetByIdAsync(sessionId, ct);
        if (session is null)
            return new ProcessCallbackResponse(sessionId, false, Fallback(sessionId));

        if (session.Provider != command.Provider)
        {
            _logger.LogWarning(
                "Funding callback provider mismatch. FundingSessionId={FundingSessionId} ExpectedProvider={ExpectedProvider} ActualProvider={ActualProvider}",
                sessionId, session.Provider, command.Provider);
            return Failure(session);
        }

        if (session.IsTerminal())
            return session.Status == PaymentSessionStatus.Succeeded
                ? Success(session)
                : Failure(session);

        if (!command.IsRecovery && !CallbackIdentityMatches(session, command))
        {
            _logger.LogWarning(
                "Funding callback attempt mismatch. FundingSessionId={FundingSessionId} Provider={Provider}",
                session.Id, session.Provider);
            return Failure(session);
        }

        var allowed = command.IsRecovery
            ? new[] { PaymentSessionStatus.TokenReceived, PaymentSessionStatus.PendingRecovery,
                PaymentSessionStatus.Processing }
            : new[] { PaymentSessionStatus.TokenReceived, PaymentSessionStatus.CallbackReceived,
                PaymentSessionStatus.PendingRecovery };
        var now = DateTimeOffset.UtcNow;
        if (!await _sessions.TryClaimAsync(sessionId, allowed, now,
            now.AddSeconds(Math.Max(30, _options.ProcessingLeaseSeconds)), ct))
        {
            session = await _sessions.GetByIdAsync(sessionId, ct) ?? session;
            return session.Status == PaymentSessionStatus.Succeeded
                ? Success(session)
                : Failure(session);
        }

        session = await _sessions.GetByIdAsync(sessionId, ct)
            ?? throw new InvalidOperationException("جلسه پس از claim یافت نشد.");

        if (!command.IsRecovery)
            session.RecordCallback(command.RefNum, command.TraceNo, MaskPan(command.SecurePan),
                command.RawCallbackJson);

        var transactionId = command.RefNum ?? session.ProviderTransactionId;
        if (string.IsNullOrWhiteSpace(transactionId))
            return await PendAsync(session, null,
                "شناسه تراکنش درگاه هنوز مشخص نیست.", ct);

        try
        {
            session.BindProviderTransaction(transactionId);
            await _sessions.UpdateAsync(session, ct); // uniqueness is owned before verify/top-up
        }
        catch (ProviderTransactionAlreadyClaimedException)
        {
            session = await _sessions.GetByIdAsync(sessionId, ct)
                ?? throw new InvalidOperationException("جلسه تکراری قابل بازیابی نیست.");
            session.MarkAsFailed(null, "تراکنش درگاه قبلاً به جلسه دیگری متصل شده است.");
            await _sessions.UpdateAsync(session, ct);
            return Failure(session);
        }

        var provider = _providers.GetProvider(session.Provider);
        VerifyResult verified;
        if (command.IsRecovery)
        {
            if (provider is not IRecoverablePaymentGatewayProvider recoverable)
                return await PendAsync(session, null,
                    "درگاه قابلیت استعلام خودکار ندارد.", ct);
            verified = await recoverable.InquireAsync(new FundingInquiryRequest(
                session.ProviderAttemptId!, session.ProviderTransactionId!, session.Id.ToString(),
                session.AmountMinor, session.Currency), ct);
        }
        else
        {
            verified = await provider.VerifyAsync(new VerifyRequest(
                session.ProviderTransactionId!, session.ProviderAttemptId!, session.Id.ToString(),
                session.AmountMinor, session.Currency), ct);
        }

        if (verified.Outcome is FundingProviderOutcome.Pending or FundingProviderOutcome.Unknown)
            return await PendAsync(session, verified.ResultCode,
                verified.ErrorMessage ?? "نتیجه تراکنش درگاه هنوز قطعی نیست.", ct);

        if (verified.Outcome == FundingProviderOutcome.DefinitiveFailure)
        {
            session.MarkAsFailed(verified.ResultCode,
                verified.ErrorMessage ?? "درگاه تراکنش را ناموفق اعلام کرد.");
            await _sessions.UpdateAsync(session, ct);
            return Failure(session);
        }

        var mismatch = ValidateVerifiedIdentity(session, verified);
        if (mismatch is not null)
            return await FailWithCompensationAsync(session, provider, verified.ResultCode,
                mismatch, ct);

        CommandResponse<TopUpWalletResponse> response;
        try
        {
            response = await _mediator.Send(new TopUpWalletCommand(
                session.WalletId, session.AmountMinor, session.Currency,
                $"funding-{session.Provider}-{session.ProviderTransactionId}",
                null, session.ProviderTransactionId), ct);
        }
        catch (Exception ex) when (IsDefinitiveWalletFailure(ex))
        {
            return await FailWithCompensationAsync(session, provider, verified.ResultCode,
                $"کیف‌پول عملیات را به‌طور قطعی رد کرد: {ex.GetType().Name}", ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Wallet funding outcome unknown. FundingSessionId={FundingSessionId} ProviderTransactionId={ProviderTransactionId}",
                session.Id, session.ProviderTransactionId);
            return await PendAsync(session, verified.ResultCode,
                "نتیجه ثبت اعتبار کیف‌پول نامشخص است.", ct);
        }

        if (response.Status == CommandStatus.InProgress)
            return await PendAsync(session, verified.ResultCode,
                "عملیات کیف‌پول در حال پردازش است.", ct);
        if (response.Status != CommandStatus.Completed || response.Data is null)
            return await PendAsync(session, verified.ResultCode,
                "نتیجه عملیات کیف‌پول مشخص نیست.", ct);

        session.MarkAsSucceeded(response.Data.LedgerEntryId, verified.ResultCode);
        // Intentionally outside the wallet exception boundary. If this write fails,
        // the durable Processing lease expires and recovery replays the same wallet
        // idempotency key instead of trying to mutate an in-memory terminal aggregate.
        await _sessions.UpdateAsync(session, ct);
        _logger.LogInformation(
            "Wallet funding succeeded. FundingSessionId={FundingSessionId} Provider={Provider} ProviderTransactionId={ProviderTransactionId} WalletId={WalletId}",
            session.Id, session.Provider, session.ProviderTransactionId, session.WalletId);
        return Success(session);
    }

    private async Task<ProcessCallbackResponse> PendAsync(
        PaymentGatewaySession session, int? code, string reason, CancellationToken ct)
    {
        var exponent = Math.Min(session.RecoveryAttemptCount, 6);
        var delay = TimeSpan.FromSeconds(
            Math.Max(5, _options.RecoveryBaseDelaySeconds) * Math.Pow(2, exponent));
        session.MarkPendingRecovery(code, reason, DateTimeOffset.UtcNow.Add(delay));
        await _sessions.UpdateAsync(session, ct);
        _logger.LogWarning(
            "Wallet funding pending recovery. FundingSessionId={FundingSessionId} Provider={Provider} ProviderTransactionId={ProviderTransactionId} RecoveryAttempt={RecoveryAttempt} Outcome={Outcome}",
            session.Id, session.Provider, session.ProviderTransactionId,
            session.RecoveryAttemptCount, reason);
        return Failure(session);
    }

    private async Task<ProcessCallbackResponse> FailWithCompensationAsync(
        PaymentGatewaySession session,
        IPaymentGatewayProvider provider,
        int code,
        string reason,
        CancellationToken ct)
    {
        var reversible = provider as IReversiblePaymentGatewayProvider;
        session.RequireReversal(reason, reversible is not null, DateTimeOffset.UtcNow);
        if (reversible is null)
        {
            session.MarkPendingRecovery(code,
                $"ناسازگاری مالی نیازمند رسیدگی عملیاتی است و درگاه reverse ندارد: {reason}",
                DateTimeOffset.UtcNow.AddHours(1));
            await _sessions.UpdateAsync(session, ct);
            return Failure(session);
        }

        session.MarkAsFailed(code, reason);
        await _sessions.UpdateAsync(session, ct);

        if (session.ProviderTransactionId is not null)
        {
            var result = await reversible.ReverseAsync(
                new ReverseRequest(session.ProviderTransactionId), ct);
            session.RecordReversalResult(result.IsSuccess,
                result.ErrorMessage ?? reason,
                result.IsSuccess ? null : DateTimeOffset.UtcNow.AddMinutes(1));
            await _sessions.UpdateAsync(session, ct);
        }

        return Failure(session);
    }

    private static string? ValidateVerifiedIdentity(
        PaymentGatewaySession session,
        VerifyResult verified)
    {
        if (!string.Equals(verified.ProviderTransactionId,
            session.ProviderTransactionId, StringComparison.Ordinal))
            return "شناسه تراکنش تأییدشده با جلسه تطابق ندارد.";
        if (verified.VerifiedAmountMinor != session.AmountMinor)
            return $"مبلغ تأییدشده با جلسه تطابق ندارد. Expected={session.AmountMinor} Actual={verified.VerifiedAmountMinor}.";
        if (!string.Equals(verified.VerifiedCurrency, session.Currency,
            StringComparison.OrdinalIgnoreCase))
            return "ارز تأییدشده با جلسه تطابق ندارد.";
        if (session.Provider == PaymentGatewayProviderType.Jibit &&
            !string.Equals(verified.ClientReference, session.Id.ToString(),
                StringComparison.Ordinal))
            return "مرجع مشتری جیبیت با جلسه تطابق ندارد.";
        return null;
    }

    private static bool CallbackIdentityMatches(
        PaymentGatewaySession session,
        ProcessCallbackCommand command)
    {
        if (string.IsNullOrWhiteSpace(session.ProviderAttemptId))
            return false;
        return session.Provider switch
        {
            PaymentGatewayProviderType.Jibit => string.Equals(
                command.RefNum, session.ProviderAttemptId, StringComparison.Ordinal),
            PaymentGatewayProviderType.Sep => string.Equals(
                command.Token, session.ProviderAttemptId, StringComparison.Ordinal),
            _ => false,
        };
    }

    private static bool IsDefinitiveWalletFailure(Exception ex) =>
        ex is WalletNotFoundException or WalletCurrencyMismatchException
            or WalletOperationNotAllowedException or IdempotencyKeyConflictException
            or FluentValidation.ValidationException;

    private static string? MaskPan(string? pan)
    {
        if (string.IsNullOrWhiteSpace(pan))
            return null;
        var normalized = new string(pan.Where(char.IsDigit).ToArray());
        return normalized.Length >= 4 ? $"****{normalized[^4..]}" : "****";
    }

    private static ProcessCallbackResponse Success(PaymentGatewaySession session) =>
        new(session.Id, true, session.BuildSuccessRedirectUrl());
    private static ProcessCallbackResponse Failure(PaymentGatewaySession session) =>
        new(session.Id, false, session.BuildFailureRedirectUrl());
    private static string Fallback(Guid id) => $"/charge/wallet/topup/result/{id}";
}
