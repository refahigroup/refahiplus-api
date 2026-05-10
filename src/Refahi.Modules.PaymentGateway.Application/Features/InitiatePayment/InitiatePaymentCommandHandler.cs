using MediatR;
using Microsoft.Extensions.Logging;
using Refahi.Modules.PaymentGateway.Application.Contracts.Exceptions;
using Refahi.Modules.PaymentGateway.Application.Contracts.Features.InitiatePayment;
using Refahi.Modules.PaymentGateway.Application.Contracts.Providers;
using Refahi.Modules.PaymentGateway.Application.Contracts.Repositories;
using Refahi.Modules.PaymentGateway.Domain.Aggregates;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Refahi.Modules.PaymentGateway.Application.Features.InitiatePayment;

public class InitiatePaymentCommandHandler : IRequestHandler<InitiatePaymentCommand, InitiatePaymentResponse>
{
    private readonly IPaymentGatewaySessionRepository _sessionRepository;
    private readonly IPaymentGatewayProviderFactory _providerFactory;
    private readonly ILogger<InitiatePaymentCommandHandler> _logger;

    public InitiatePaymentCommandHandler(
        IPaymentGatewaySessionRepository sessionRepository,
        IPaymentGatewayProviderFactory providerFactory,
        ILogger<InitiatePaymentCommandHandler> logger)
    {
        _sessionRepository = sessionRepository;
        _providerFactory = providerFactory;
        _logger = logger;
    }

    public async Task<InitiatePaymentResponse> Handle(InitiatePaymentCommand command, CancellationToken ct)
    {
        var sessionId = Guid.NewGuid();

        _logger.LogInformation(
            "PaymentGateway: Initiating session {SessionId} for User={UserId} Wallet={WalletId} Amount={Amount} Provider={Provider}",
            sessionId, command.UserId, command.WalletId, command.AmountMinor, command.Provider);

        var session = PaymentGatewaySession.Create(
            sessionId: sessionId,
            userId: command.UserId,
            walletId: command.WalletId,
            amountMinor: command.AmountMinor,
            currency: command.Currency,
            provider: command.Provider,
            returnBaseUrl: command.ReturnBaseUrl,
            succeededCallbackUrl: command.SucceededCallbackUrl,
            failedCallbackUrl: command.FailedCallbackUrl);

        await _sessionRepository.AddAsync(session, ct);

        var provider = _providerFactory.GetProvider(command.Provider);

        var tokenResult = await provider.GetTokenAsync(
            new GetTokenRequest(
                ResNum: sessionId.ToString(),
                AmountMinor: command.AmountMinor,
                CallbackUrl: command.ProviderCallbackUrl),
            ct);

        if (!tokenResult.IsSuccess || string.IsNullOrEmpty(tokenResult.Token))
        {
            _logger.LogWarning(
                "PaymentGateway: Token request failed for Session={SessionId}. Error={Error}",
                sessionId, tokenResult.ErrorMessage);

            session.MarkAsFailed(null, tokenResult.ErrorMessage ?? "خطا در دریافت توکن از درگاه پرداخت");
            await _sessionRepository.UpdateAsync(session, ct);

            throw new PaymentTokenRequestFailedException(
                tokenResult.ErrorMessage ?? "خطا در دریافت توکن از درگاه پرداخت");
        }

        session.MarkAsTokenReceived(tokenResult.Token);
        await _sessionRepository.UpdateAsync(session, ct);

        var gatewayRedirectUrl = provider.BuildRedirectUrl(tokenResult.Token);

        _logger.LogInformation(
            "PaymentGateway: Session {SessionId} ready. Redirecting to {Url}",
            sessionId, gatewayRedirectUrl);

        return new InitiatePaymentResponse(sessionId, gatewayRedirectUrl);
    }

}
