using MediatR;
using Refahi.Modules.Entertainments.Application.Features.Checkout.Sessions;
using Refahi.Modules.Entertainments.Domain;
using Refahi.Modules.Entertainments.Application.Contracts.Providers;
using Refahi.Modules.Entertainments.Application.Contracts;
using Refahi.Modules.Entertainments.Application.Exceptions;
using System.Text.Json;
namespace Refahi.Modules.Entertainments.Application.Features.Checkout.Prepare;

public sealed class PrepareEntertainmentCheckoutCommandHandler(IEntertainmentRepository repository, IEntertainmentSessionRepository sessions, IMediator mediator,
    IEntertainmentProviderFactory providers, IEntertainmentSecretProtector secrets)
    : IRequestHandler<PrepareEntertainmentCheckoutCommand, PrepareEntertainmentCheckoutResponse>
{
    public async Task<PrepareEntertainmentCheckoutResponse> Handle(PrepareEntertainmentCheckoutCommand request, CancellationToken ct)
    {
        var old = await sessions.FindAsync(request.UserId, request.IdempotencyKey.Trim(), ct);
        var cart = await repository.GetCartAsync(request.UserId, ct);
        var keys = cart?.Items.Select(x => x.ProviderKey).Distinct(StringComparer.OrdinalIgnoreCase).ToArray() ?? [];
        var provider = request.ProviderKey ?? old?.ProviderKey ?? (keys.Length == 1 ? keys[0] : null);
        if (provider == null)
            throw new EntertainmentDomainException("بخش مربوط به یک پرووایدر را برای پرداخت انتخاب کنید", "PROVIDER_SELECTION_REQUIRED");
        // Reservation providers require an explicit review of the actual reserved amount.
        if (providers.GetRequired(provider) is IEntertainmentReservationProvider)
            throw new EntertainmentDomainException("رزرو را در صفحه بازبینی تأیید کنید", "SESSION_REVIEW_REQUIRED");
        var session = await mediator.Send(new StartEntertainmentSessionCommand(request.UserId, provider, request.IdempotencyKey, []), ct);
        var stored = await sessions.GetAsync(session.Id, ct) ?? throw new InvalidOperationException();
        if (stored.EntertainmentPurchaseId == null)
        {
            var reserved = JsonSerializer.Deserialize<EntertainmentReservationResult>(secrets.Unprotect(stored.ReservationProtected!))!;
            var original = JsonSerializer.Deserialize<EntertainmentReservationRequest>(secrets.Unprotect(stored.RequestProtected))!;
            var changed = reserved.Lines.FirstOrDefault(x => x.Quote.UnitPriceMinor != original.Lines.Single(y => y.CartItemId == x.CartItemId).ExpectedUnitPriceMinor);
            if (changed != null) throw new EntertainmentPriceChangedException(changed.Quote);
        }
        return await mediator.Send(new ConfirmEntertainmentSessionCommand(request.UserId, session.Id, session.Version), ct);
    }
}
