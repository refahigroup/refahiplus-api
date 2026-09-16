using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;
using Refahi.Modules.PaymentGateway.Application.Contracts.Exceptions;
using Refahi.Modules.PaymentGateway.Application.Contracts.Features.InitiatePayment;
using Refahi.Modules.PaymentGateway.Application.Contracts.Providers;
using Refahi.Modules.PaymentGateway.Application.Contracts.Repositories;
using Refahi.Modules.PaymentGateway.Domain.Aggregates;
using Refahi.Modules.Wallets.Application.Contracts.Repositories;
using Refahi.Modules.Wallets.Application.Contracts;
using Refahi.Shared.Monetary;

namespace Refahi.Modules.PaymentGateway.Application.Features.InitiatePayment;

public class InitiatePaymentCommandHandler
    : IRequestHandler<InitiatePaymentCommand, InitiatePaymentResponse>
{
    private readonly IPaymentGatewaySessionRepository _sessionRepository;
    private readonly IPaymentGatewayProviderFactory _providerFactory;
    private readonly IWalletReadRepository _walletReadRepository;
    private readonly ILogger<InitiatePaymentCommandHandler> _logger;

    public InitiatePaymentCommandHandler(
        IPaymentGatewaySessionRepository sessionRepository,
        IPaymentGatewayProviderFactory providerFactory,
        IWalletReadRepository walletReadRepository,
        ILogger<InitiatePaymentCommandHandler> logger
    )
    {
        _sessionRepository = sessionRepository;
        _providerFactory = providerFactory;
        _walletReadRepository = walletReadRepository;
        _logger = logger;
    }

    public async Task<InitiatePaymentResponse> Handle(
        InitiatePaymentCommand command,
        CancellationToken ct
    )
    {
        var wallet = await _walletReadRepository.GetByIdAsync(command.WalletId, ct);
        if (wallet is null || wallet.OwnerId != command.UserId)
        {
            throw new ValidationException([
                new FluentValidation.Results.ValidationFailure(
                    nameof(command.WalletId),
                    "کیف‌پول انتخاب‌شده متعلق به کاربر جاری نیست."
                ),
            ]);
        }

        if (wallet.WalletType != WalletTypeValues.User)
        {
            throw new ValidationException([
                new FluentValidation.Results.ValidationFailure(
                    nameof(command.WalletId),
                    "شارژ بانکی فقط برای کیف‌پول شخصی رفاهی مجاز است.")
            ]);
        }

        var currency = CurrencyCode.Parse(command.Currency).Value;
        if (!string.Equals(wallet.Currency, currency, StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning(
                "Payment gateway currency mismatch. Operation={Operation} WalletId={WalletId} ExpectedCurrency={ExpectedCurrency} ProvidedCurrency={ProvidedCurrency}",
                "InitiatePayment",
                command.WalletId,
                wallet.Currency,
                currency
            );
            throw new ValidationException([
                new FluentValidation.Results.ValidationFailure(
                    nameof(command.Currency),
                    "ارز عملیات با ارز کیف‌پول مطابقت ندارد."
                ),
            ]);
        }

        var idempotencyKey = command.IdempotencyKey!.Trim();
        var requestHash = FundingInitiationHasher.Compute(command);
        var existing = await _sessionRepository.GetByInitiationKeyAsync(
            command.UserId, idempotencyKey, ct);
        if (existing is not null)
            return await ReturnExistingAsync(existing, requestHash, ct);

        var sessionId = Guid.NewGuid();

        _logger.LogInformation(
            "PaymentGateway: Initiating session {SessionId} for User={UserId} Wallet={WalletId} Amount={Amount} Provider={Provider} ProviderCallbackUrl={ProviderCallbackUrl}",
            sessionId,
            command.UserId,
            command.WalletId,
            command.AmountMinor,
            command.Provider,
            command.ProviderCallbackUrl
        );

        var session = PaymentGatewaySession.Create(
            sessionId: sessionId,
            userId: command.UserId,
            walletId: command.WalletId,
            amountMinor: command.AmountMinor,
            currency: currency,
            provider: command.Provider,
            returnBaseUrl: command.ReturnBaseUrl,
            succeededCallbackUrl: command.SucceededCallbackUrl,
            failedCallbackUrl: command.FailedCallbackUrl,
            initiationIdempotencyKey: idempotencyKey,
            initiationRequestHash: requestHash,
            initiationRequestHashVersion: FundingInitiationHasher.Version
        );

        if (!await _sessionRepository.TryAddAsync(session, ct))
        {
            existing = await _sessionRepository.GetByInitiationKeyAsync(
                command.UserId, idempotencyKey, ct)
                ?? throw new InvalidOperationException("جلسه تکراری قابل بازیابی نیست.");
            return await ReturnExistingAsync(existing, requestHash, ct);
        }

        var provider = _providerFactory.GetProvider(command.Provider);

        var tokenResult = await provider.GetTokenAsync(
            new GetTokenRequest(
                ResNum: sessionId.ToString(),
                AmountMinor: command.AmountMinor,
                CallbackUrl: command.ProviderCallbackUrl
            ),
            ct
        );

        if (!tokenResult.IsSuccess || string.IsNullOrEmpty(tokenResult.Token)
            || string.IsNullOrWhiteSpace(tokenResult.ProviderAttemptId))
        {
            _logger.LogWarning(
                "PaymentGateway: Token request failed for Session={SessionId}. Error={Error}",
                sessionId,
                tokenResult.ErrorMessage
            );

            if (tokenResult.IsOutcomeUnknown)
                session.MarkPendingRecovery(null,
                    tokenResult.ErrorMessage ?? "نتیجه ایجاد درخواست درگاه نامشخص است.",
                    DateTimeOffset.UtcNow.AddMinutes(1));
            else
                session.MarkAsFailed(null,
                    tokenResult.ErrorMessage ?? "خطا در دریافت توکن از درگاه پرداخت");
            await _sessionRepository.UpdateAsync(session, ct);

            throw new PaymentTokenRequestFailedException(
                tokenResult.ErrorMessage ?? "خطا در دریافت توکن از درگاه پرداخت"
            );
        }

        session.MarkAsTokenReceived(
            tokenResult.Token,
            tokenResult.ProviderAttemptId,
            tokenResult.ProviderTransactionId);
        await _sessionRepository.UpdateAsync(session, ct);

        var gatewayRedirectUrl = provider.BuildRedirectUrl(tokenResult.Token);

        _logger.LogInformation(
            "PaymentGateway: Session {SessionId} ready. Redirecting to {Url}",
            sessionId,
            gatewayRedirectUrl
        );

        return new InitiatePaymentResponse(sessionId, gatewayRedirectUrl);
    }

    private async Task<InitiatePaymentResponse> ReturnExistingAsync(
        PaymentGatewaySession session,
        string requestHash,
        CancellationToken ct)
    {
        if (!session.HasEquivalentInitiation(requestHash, FundingInitiationHasher.Version))
            throw new FundingIdempotencyConflictException();

        for (var attempt = 0; attempt < 40 && session.Status ==
            Domain.Enums.PaymentSessionStatus.Initiated; attempt++)
        {
            await Task.Delay(50, ct);
            session = await _sessionRepository.GetByIdAsync(session.Id, ct) ?? session;
        }

        if (!string.IsNullOrWhiteSpace(session.ProviderToken))
        {
            var provider = _providerFactory.GetProvider(session.Provider);
            return new InitiatePaymentResponse(
                session.Id, provider.BuildRedirectUrl(session.ProviderToken));
        }

        throw new PaymentTokenRequestFailedException(
            session.Status == Domain.Enums.PaymentSessionStatus.PendingRecovery
                ? "نتیجه ایجاد درخواست درگاه در حال بررسی است."
                : "درخواست درگاه با این کلید تکرار تکمیل نشده است.");
    }
}
