using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Refahi.Modules.PaymentGateway.Application.Contracts.Features.GetPaymentSession;
using Refahi.Modules.PaymentGateway.Application.Contracts.Features.InitiatePayment;
using Refahi.Modules.PaymentGateway.Application.Contracts.Funding;
using Refahi.Modules.PaymentGateway.Domain.Enums;
using Microsoft.Extensions.Options;
using Refahi.Modules.PaymentGateway.Application.Contracts.Configuration;
using Refahi.Modules.PaymentGateway.Application.Contracts.Repositories;

namespace Refahi.Modules.PaymentGateway.Application.Funding;

internal sealed class WalletFundingService(
    IMediator mediator,
    IPaymentGatewaySessionRepository sessions,
    IOptions<FundingSafetyOptions> options) : IWalletFundingService
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
        if (result is null) return null;
        var session = await sessions.GetByIdAsync(result.SessionId, ct);
        return session is null ? null : Map(session);
    }

    public Task<CreateWalletFundingResult> CreateBoundAsync(
        CreateBoundWalletFundingRequest request,
        CancellationToken ct = default)
    {
        var settings = options.Value;
        if (!Enum.TryParse<PaymentGatewayProviderType>(settings.DefaultProvider, true, out var provider))
            throw new InvalidOperationException("درگاه پیش‌فرض تأمین مالی معتبر نیست.");
        if (!Uri.TryCreate(settings.PublicBaseUrl, UriKind.Absolute, out var publicBase))
            throw new InvalidOperationException("نشانی عمومی WalletFunding پیکربندی نشده است.");
        var callback = PaymentGatewayCallbackUrlBuilder.Build(
            provider, publicBase.ToString(), publicBase.Scheme, publicBase.Authority,
            allowRequestFallback: false);
        return CreateAsync(new CreateWalletFundingRequest(
            request.UserId, request.WalletId, request.AmountMinor, request.Currency,
            provider.ToString(), callback, request.ReturnUrl, request.ClientReference,
            request.ReturnUrl, request.ReturnUrl), ct);
    }

    public async Task<WalletFundingStatus?> GetStatusByClientReferenceAsync(
        Guid userId,
        string clientReference,
        CancellationToken ct = default)
    {
        var session = await sessions.GetByInitiationKeyAsync(userId, clientReference, ct);
        return session is null ? null : Map(session);
    }

    private static WalletFundingStatus Map(Domain.Aggregates.PaymentGatewaySession session) => new(
        FundingSessionId: session.Id,
        WalletId: session.WalletId,
        AmountMinor: session.AmountMinor,
        Currency: session.Currency,
        Status: session.Status.ToString(),
        WalletLedgerEntryId: session.TopUpLedgerEntryId,
        UserId: session.UserId,
        ClientReference: session.InitiationIdempotencyKey ?? string.Empty);
}
