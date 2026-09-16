using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Refahi.Modules.PaymentGateway.Application.Contracts.Configuration;
using Refahi.Modules.PaymentGateway.Application.Contracts.Features.ProcessCallback;
using Refahi.Modules.PaymentGateway.Application.Contracts.Features.Recovery;
using Refahi.Modules.PaymentGateway.Application.Contracts.Providers;
using Refahi.Modules.PaymentGateway.Application.Contracts.Repositories;
using Refahi.Modules.PaymentGateway.Domain.Enums;

namespace Refahi.Modules.PaymentGateway.Application.Features.Recovery;

public sealed class RecoverFundingSessionCommandHandler
    : IRequestHandler<RecoverFundingSessionCommand>
{
    private readonly IPaymentGatewaySessionRepository _sessions;
    private readonly IMediator _mediator;
    private readonly IPaymentGatewayProviderFactory _providers;
    private readonly FundingSafetyOptions _options;

    public RecoverFundingSessionCommandHandler(
        IPaymentGatewaySessionRepository sessions,
        IMediator mediator,
        IPaymentGatewayProviderFactory providers,
        IOptions<FundingSafetyOptions> options)
    {
        _sessions = sessions;
        _mediator = mediator;
        _providers = providers;
        _options = options.Value;
    }

    public async Task<Unit> Handle(RecoverFundingSessionCommand command, CancellationToken ct)
    {
        var session = await _sessions.GetByIdAsync(command.SessionId, ct);
        if (session is null || session.IsTerminal())
            return Unit.Value;

        if (string.IsNullOrWhiteSpace(session.ProviderTransactionId) &&
            session.Provider == PaymentGatewayProviderType.Jibit &&
            _providers.GetProvider(session.Provider) is IRecoverablePaymentGatewayProvider recoverable)
        {
            var inquiry = await recoverable.InquireAsync(new FundingInquiryRequest(
                session.ProviderAttemptId, null, session.Id.ToString(),
                session.AmountMinor, session.Currency), ct);
            if (!string.IsNullOrWhiteSpace(inquiry.ProviderTransactionId))
            {
                session.BindRecoveredProviderIdentity(inquiry.ProviderTransactionId,
                    inquiry.ProviderTransactionId);
                await _sessions.UpdateAsync(session, ct);
            }
        }

        if (DateTimeOffset.UtcNow - session.InitiatedAt >
            TimeSpan.FromHours(Math.Max(1, _options.RecoveryMaxAgeHours)) &&
            string.IsNullOrWhiteSpace(session.ProviderTransactionId))
        {
            session.MarkAsFailed(null,
                "مهلت بازیابی پایان یافت و هیچ شناسه تراکنش خارجی ثبت نشده است.");
            await _sessions.UpdateAsync(session, ct);
            return Unit.Value;
        }

        if (session.Status == PaymentSessionStatus.TokenReceived && session.IsExpired() &&
            string.IsNullOrWhiteSpace(session.ProviderTransactionId))
        {
            session.MarkAsExpired();
            await _sessions.UpdateAsync(session, ct);
            return Unit.Value;
        }

        if (string.IsNullOrWhiteSpace(session.ProviderTransactionId))
        {
            var next = DateTimeOffset.UtcNow.AddSeconds(
                Math.Max(5, _options.RecoveryBaseDelaySeconds));
            session.MarkPendingRecovery(null,
                "شناسه تراکنش خارجی هنوز از درگاه بازیابی نشده است.", next);
            await _sessions.UpdateAsync(session, ct);
            return Unit.Value;
        }

        await _mediator.Send(new ProcessCallbackCommand(
            session.Provider,
            "RECOVERY",
            session.ProviderTransactionId,
            session.Id.ToString(),
            session.ProviderTraceNo,
            session.ProviderSecurePan,
            null,
            Token: session.ProviderAttemptId,
            IsRecovery: true), ct);
        return Unit.Value;
    }
}

public sealed class RetryFundingReversalCommandHandler
    : IRequestHandler<RetryFundingReversalCommand>
{
    private readonly IPaymentGatewaySessionRepository _sessions;
    private readonly IPaymentGatewayProviderFactory _providers;
    private readonly ILogger<RetryFundingReversalCommandHandler> _logger;

    public RetryFundingReversalCommandHandler(
        IPaymentGatewaySessionRepository sessions,
        IPaymentGatewayProviderFactory providers,
        ILogger<RetryFundingReversalCommandHandler> logger)
    {
        _sessions = sessions;
        _providers = providers;
        _logger = logger;
    }

    public async Task<Unit> Handle(RetryFundingReversalCommand command, CancellationToken ct)
    {
        if (!await _sessions.TryClaimReversalAsync(command.SessionId, ct))
            return Unit.Value;
        var session = await _sessions.GetByIdAsync(command.SessionId, ct);
        if (session?.ProviderTransactionId is null)
            return Unit.Value;
        var provider = _providers.GetProvider(session.Provider);
        if (provider is not IReversiblePaymentGatewayProvider reversible)
        {
            session.RecordReversalResult(false, "درگاه قابلیت reverse ندارد.", null);
            await _sessions.UpdateAsync(session, ct);
            return Unit.Value;
        }

        var result = await reversible.ReverseAsync(
            new ReverseRequest(session.ProviderTransactionId), ct);
        session.RecordReversalResult(result.IsSuccess, result.ErrorMessage,
            result.IsSuccess ? null : DateTimeOffset.UtcNow.AddMinutes(5));
        await _sessions.UpdateAsync(session, ct);
        _logger.LogInformation(
            "Funding reversal processed. FundingSessionId={FundingSessionId} Provider={Provider} ProviderTransactionId={ProviderTransactionId} ReversalStatus={ReversalStatus}",
            session.Id, session.Provider, session.ProviderTransactionId, session.ReversalStatus);
        return Unit.Value;
    }
}
