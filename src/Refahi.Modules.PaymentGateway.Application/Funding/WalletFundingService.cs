using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Refahi.Modules.PaymentGateway.Application.Contracts.Features.GetPaymentSession;
using Refahi.Modules.PaymentGateway.Application.Contracts.Features.InitiatePayment;
using Refahi.Modules.PaymentGateway.Application.Contracts.Funding;
using Refahi.Modules.PaymentGateway.Domain.Enums;

namespace Refahi.Modules.PaymentGateway.Application.Funding;

internal sealed class WalletFundingService(IMediator mediator) : IWalletFundingService
{
    public async Task<CreateWalletFundingResult> CreateAsync(
        CreateWalletFundingRequest request,
        CancellationToken ct = default)
    {
        if (!Enum.TryParse<PaymentGatewayProviderType>(request.FundingProvider, true,
            out var provider))
            throw new ArgumentException("درگاه تأمین مالی پشتیبانی نمی‌شود.",
                nameof(request.FundingProvider));
        var result = await mediator.Send(new InitiatePaymentCommand(
            request.UserId, request.WalletId, request.AmountMinor, request.Currency,
            provider, request.ProviderCallbackUrl, request.ReturnBaseUrl,
            request.SucceededCallbackUrl, request.FailedCallbackUrl,
            request.IdempotencyKey), ct);
        return new CreateWalletFundingResult(result.SessionId, result.GatewayRedirectUrl);
    }

    public async Task<WalletFundingStatus?> GetStatusAsync(
        Guid userId,
        Guid fundingSessionId,
        CancellationToken ct = default)
    {
        var result = await mediator.Send(
            new GetPaymentSessionQuery(fundingSessionId, userId), ct);
        return result is null ? null : new WalletFundingStatus(
            result.SessionId, result.WalletId, result.AmountMinor, result.Currency,
            result.Status.ToString(), result.TopUpLedgerEntryId);
    }
}
