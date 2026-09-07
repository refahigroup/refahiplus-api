using MediatR;
using Refahi.Modules.Commerce.Application.Features.Checkout.Sessions;
using Refahi.Modules.Commerce.Domain;
using Refahi.Modules.Commerce.Application.Contracts.Providers;
using Refahi.Modules.Commerce.Application.Contracts;
using Refahi.Modules.Commerce.Application.Exceptions;
using System.Text.Json;
namespace Refahi.Modules.Commerce.Application.Features.Checkout.Prepare;

public sealed class PrepareCommerceCheckoutCommandHandler(ICommerceRepository repository, ICommerceSessionRepository sessions, IMediator mediator,
    ICommerceProviderFactory providers, ICommerceSecretProtector secrets)
    : IRequestHandler<PrepareCommerceCheckoutCommand, PrepareCommerceCheckoutResponse>
{
    public async Task<PrepareCommerceCheckoutResponse> Handle(PrepareCommerceCheckoutCommand request, CancellationToken ct)
    {
        var old = await sessions.FindAsync(request.UserId, request.IdempotencyKey.Trim(), ct);
        var cart = await repository.GetCartAsync(request.UserId, ct);
        var keys = cart?.Items.Select(x => x.ProviderKey).Distinct(StringComparer.OrdinalIgnoreCase).ToArray() ?? [];
        var provider = request.ProviderKey ?? old?.ProviderKey ?? (keys.Length == 1 ? keys[0] : null);
        if (provider == null)
            throw new CommerceDomainException("بخش مربوط به یک پرووایدر را برای پرداخت انتخاب کنید", "PROVIDER_SELECTION_REQUIRED");
        // Reservation providers require an explicit review of the actual reserved amount.
        if (providers.GetRequired(provider) is ICommerceReservationProvider)
            throw new CommerceDomainException("رزرو را در صفحه بازبینی تأیید کنید", "SESSION_REVIEW_REQUIRED");
        var session = await mediator.Send(new StartCommerceSessionCommand(request.UserId, provider, request.IdempotencyKey, []), ct);
        var stored = await sessions.GetAsync(session.Id, ct) ?? throw new InvalidOperationException();
        if (stored.CommerceOrderId == null)
        {
            var reserved = JsonSerializer.Deserialize<CommerceReservationResult>(secrets.Unprotect(stored.ReservationProtected!))!;
            var original = JsonSerializer.Deserialize<CommerceReservationRequest>(secrets.Unprotect(stored.RequestProtected))!;
            var changed = reserved.Lines.FirstOrDefault(x => x.Quote.UnitPriceMinor != original.Lines.Single(y => y.CartItemId == x.CartItemId).ExpectedUnitPriceMinor);
            if (changed != null) throw new CommercePriceChangedException(changed.Quote);
        }
        return await mediator.Send(new ConfirmCommerceSessionCommand(request.UserId, session.Id, session.Version), ct);
    }
}
