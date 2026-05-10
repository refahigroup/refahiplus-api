using MediatR;
using Microsoft.Extensions.Logging;
using Refahi.Modules.PaymentGateway.Application.Contracts.Exceptions;
using Refahi.Modules.PaymentGateway.Application.Contracts.Features.ProcessCallback;
using Refahi.Modules.PaymentGateway.Application.Contracts.Providers;
using Refahi.Modules.PaymentGateway.Application.Contracts.Repositories;
using Refahi.Modules.PaymentGateway.Domain.Exceptions;
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

                var topUpResponse = await _mediator.Send(topUpCommand, ct);

                var ledgerEntryId = topUpResponse.Data?.LedgerEntryId ?? Guid.Empty;
                session.MarkAsSucceeded(ledgerEntryId, verifyResult.ResultCode);
                await _sessionRepository.UpdateAsync(session, ct);

                _logger.LogInformation(
                    "PaymentGateway: Wallet topped up. Session={SessionId} LedgerEntry={LedgerEntryId}",
                    sessionId, ledgerEntryId);

                return new ProcessCallbackResponse(sessionId, true, session.BuildSuccessRedirectUrl());
            }
            else
            {
                _logger.LogWarning(
                    "PaymentGateway: Verification failed. Session={SessionId} Code={Code} Desc={Desc}",
                    sessionId, verifyResult.ResultCode, verifyResult.ErrorMessage);

                session.MarkAsFailed(verifyResult.ResultCode, verifyResult.ErrorMessage);
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
}
