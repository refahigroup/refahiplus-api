using MediatR;
using Microsoft.Extensions.Logging;
using Refahi.Modules.PaymentGateway.Application.Contracts.Exceptions;
using Refahi.Modules.PaymentGateway.Application.Contracts.Features.ProcessCallback;
using Refahi.Modules.PaymentGateway.Application.Contracts.Providers;
using Refahi.Modules.PaymentGateway.Application.Contracts.Repositories;
using Refahi.Modules.PaymentGateway.Domain.Exceptions;
using Refahi.Modules.Wallets.Application.Contracts;
using Refahi.Modules.Wallets.Application.Contracts.Features.TopUp;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Refahi.Modules.PaymentGateway.Application.Features.ProcessCallback;

public class ProcessCallbackCommandHandler : IRequestHandler<ProcessCallbackCommand, ProcessCallbackResponse>
{
    private readonly IPaymentGatewaySessionRepository _sessionRepository;
    private readonly IPaymentGatewayProviderFactory _providerFactory;
    private readonly IMediator _mediator;
    private readonly ILogger<ProcessCallbackCommandHandler> _logger;

    public ProcessCallbackCommandHandler(
        IPaymentGatewaySessionRepository sessionRepository,
        IPaymentGatewayProviderFactory providerFactory,
        IMediator mediator,
        ILogger<ProcessCallbackCommandHandler> logger)
    {
        _sessionRepository = sessionRepository;
        _providerFactory = providerFactory;
        _mediator = mediator;
        _logger = logger;
    }

    public async Task<ProcessCallbackResponse> Handle(ProcessCallbackCommand command, CancellationToken ct)
    {
        _logger.LogInformation(
            "PaymentGateway: Callback received. Provider={Provider} ResNum={ResNum} State={State} RefNum={RefNum}",
            command.Provider, command.ResNum, command.State, command.RefNum);

        // Parse SessionId from ResNum
        if (!Guid.TryParse(command.ResNum, out var sessionId))
        {
            _logger.LogError("PaymentGateway: Invalid ResNum format: {ResNum}", command.ResNum);
            // Can't redirect properly — return a safe fallback
            return new ProcessCallbackResponse(Guid.Empty, false, "/charge/wallet/topup/result/invalid");
        }

        var session = await _sessionRepository.GetByIdAsync(sessionId, ct);
        if (session is null)
        {
            _logger.LogError("PaymentGateway: Session not found for ResNum={ResNum}", command.ResNum);
            return new ProcessCallbackResponse(sessionId, false, BuildFallbackUrl(sessionId));
        }

        // Guard: already processed (idempotent callback)
        if (session.IsTerminal())
        {
            _logger.LogWarning(
                "PaymentGateway: Session {SessionId} already in terminal state {Status}. Ignoring duplicate callback.",
                sessionId, session.Status);

            var alreadyProcessedUrl = session.Status == Domain.Enums.PaymentSessionStatus.Succeeded
                ? session.BuildSuccessRedirectUrl()
                : session.BuildFailureRedirectUrl();

            return new ProcessCallbackResponse(sessionId, session.Status == Domain.Enums.PaymentSessionStatus.Succeeded, alreadyProcessedUrl);
        }

        // Guard: expired session
        if (session.IsExpired())
        {
            _logger.LogWarning("PaymentGateway: Session {SessionId} has expired.", sessionId);
            session.MarkAsExpired();
            await _sessionRepository.UpdateAsync(session, ct);
            return new ProcessCallbackResponse(sessionId, false, session.BuildFailureRedirectUrl());
        }

        // Record callback data
        session.MarkAsCallbackReceived(
            state: command.State,
            refNum: command.RefNum,
            traceNo: command.TraceNo,
            securePan: command.SecurePan,
            rawCallbackJson: command.RawCallbackJson);

        if (command.AmountParseFailed)
        {
            const string message = "Payment callback amount is invalid.";
            _logger.LogWarning("PaymentGateway: Callback amount is invalid. Session={SessionId}", sessionId);

            session.MarkAsFailed(null, message);
            await _sessionRepository.UpdateAsync(session, ct);
            return new ProcessCallbackResponse(sessionId, false, session.BuildFailureRedirectUrl());
        }

        if (command.AmountMinor.HasValue && command.AmountMinor.Value != session.AmountMinor)
        {
            var message = $"Payment callback amount mismatch. Expected={session.AmountMinor} Actual={command.AmountMinor.Value}.";
            _logger.LogWarning(
                "PaymentGateway: Callback amount mismatch. Session={SessionId} Expected={Expected} Actual={Actual}",
                sessionId, session.AmountMinor, command.AmountMinor.Value);

            session.MarkAsFailed(null, message);
            await _sessionRepository.UpdateAsync(session, ct);
            return new ProcessCallbackResponse(sessionId, false, session.BuildFailureRedirectUrl());
        }

        // SEP success state
        if (command.State.Equals("OK", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrEmpty(command.RefNum))
        {
            var provider = _providerFactory.GetProvider(command.Provider);

            var verifyResult = await provider.VerifyAsync(
                new VerifyRequest(command.RefNum, session.AmountMinor), ct);

            if (verifyResult.IsSuccess)
            {
                _logger.LogInformation(
                    "PaymentGateway: Transaction verified. Session={SessionId} Amount={Amount}",
                    sessionId, verifyResult.VerifiedAmountMinor);

                // Top up the wallet — use RefNum as IdempotencyKey to prevent double top-up
                var topUpCommand = new TopUpWalletCommand(
                    WalletId: session.WalletId,
                    AmountMinor: session.AmountMinor,
                    Currency: session.Currency,
                    IdempotencyKey: $"pg-{command.RefNum}",
                    MetadataJson: null,
                    ExternalReference: command.RefNum);

                try
                {
                    var topUpResponse = await _mediator.Send(topUpCommand, ct);

                    if (topUpResponse.Status != CommandStatus.Completed || topUpResponse.Data is null)
                    {
                        var reason = $"Wallet top-up did not complete. Status={topUpResponse.Status}.";
                        var reverseMessage = await TryReverseAsync(provider, command.RefNum, reason, ct);
                        session.MarkAsFailed(verifyResult.ResultCode, reverseMessage);
                        await _sessionRepository.UpdateAsync(session, ct);
                        return new ProcessCallbackResponse(sessionId, false, session.BuildFailureRedirectUrl());
                    }

                    var ledgerEntryId = topUpResponse.Data.LedgerEntryId;
                    session.MarkAsSucceeded(ledgerEntryId, verifyResult.ResultCode);
                    await _sessionRepository.UpdateAsync(session, ct);

                    _logger.LogInformation(
                        "PaymentGateway: Wallet topped up. Session={SessionId} LedgerEntry={LedgerEntryId}",
                        sessionId, ledgerEntryId);

                    return new ProcessCallbackResponse(sessionId, true, session.BuildSuccessRedirectUrl());
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "PaymentGateway: Wallet top-up failed after provider verification. Session={SessionId}", sessionId);
                    var reverseMessage = await TryReverseAsync(provider, command.RefNum, ex.Message, ct);
                    session.MarkAsFailed(verifyResult.ResultCode, reverseMessage);
                    await _sessionRepository.UpdateAsync(session, ct);
                    return new ProcessCallbackResponse(sessionId, false, session.BuildFailureRedirectUrl());
                }
            }
            else
            {
                _logger.LogWarning(
                    "PaymentGateway: Verification failed. Session={SessionId} Code={Code} Desc={Desc}",
                    sessionId, verifyResult.ResultCode, verifyResult.ErrorMessage);

                var failureMessage = verifyResult.ErrorMessage;
                if (verifyResult.ResultCode == 0 && verifyResult.VerifiedAmountMinor > 0)
                    failureMessage = await TryReverseAsync(provider, command.RefNum, verifyResult.ErrorMessage ?? "Provider verification failed.", ct);

                session.MarkAsFailed(verifyResult.ResultCode, failureMessage);
                await _sessionRepository.UpdateAsync(session, ct);
                return new ProcessCallbackResponse(sessionId, false, session.BuildFailureRedirectUrl());
            }
        }
        else
        {
            // Transaction cancelled or failed by provider
            _logger.LogInformation(
                "PaymentGateway: Transaction not confirmed by provider. Session={SessionId} State={State}",
                sessionId, command.State);

            session.MarkAsFailed(null, $"تراکنش توسط درگاه تأیید نشد. وضعیت: {command.State}");
            await _sessionRepository.UpdateAsync(session, ct);
            return new ProcessCallbackResponse(sessionId, false, session.BuildFailureRedirectUrl());
        }
    }

    private static string BuildFallbackUrl(Guid sessionId) =>
        $"/charge/wallet/topup/result/{sessionId}";

    private async Task<string> TryReverseAsync(
        IPaymentGatewayProvider provider,
        string refNum,
        string reason,
        CancellationToken ct)
    {
        if (provider is not IReversiblePaymentGatewayProvider reversibleProvider)
            return reason;

        var reverseResult = await reversibleProvider.ReverseAsync(new ReverseRequest(refNum), ct);

        if (reverseResult.IsSuccess)
        {
            _logger.LogWarning(
                "PaymentGateway: Reversed provider transaction after local failure. RefNum={RefNum} Reason={Reason}",
                refNum, reason);
            return $"{reason} Provider transaction reversed.";
        }

        _logger.LogError(
            "PaymentGateway: Provider reverse failed. RefNum={RefNum} Code={Code} Error={Error} Reason={Reason}",
            refNum, reverseResult.ResultCode, reverseResult.ErrorMessage, reason);

        return $"{reason} Provider reverse failed. Code={reverseResult.ResultCode} Error={reverseResult.ErrorMessage}";
    }
}
