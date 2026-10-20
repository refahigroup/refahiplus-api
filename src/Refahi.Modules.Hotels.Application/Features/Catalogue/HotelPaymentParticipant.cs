using Refahi.Modules.Hotels.Domain.Abstraction.Repositories;
using Refahi.Modules.Hotels.Domain.Aggregates.Catalogue;
using Refahi.Modules.Orders.Application.Contracts.Payments;
using Microsoft.Extensions.Options;
using Refahi.Modules.Hotels.Application.Contracts.Catalogue;
namespace Refahi.Modules.Hotels.Application.Features.Catalogue;

public sealed class HotelPaymentParticipant(IHotelCatalogueRepository catalogue, IHotelRequestRepository requests, IOptions<HotelLifecycleOptions>? lifecycle = null) : IOrderPaymentParticipant
{
    public string SourceModule => "Hotel";
    public async Task<string?> GetUnavailableReasonAsync(OrderPaymentContext context, CancellationToken ct)
    {
        if (context.SourceReferenceId is not Guid id) return "مرجع رزرو هتل موجود نیست.";
        var request = await requests.GetAsync(id, ct);
        if (request is null || request.UserId != context.UserId || request.OrderId != context.OrderId || request.TotalPrice != context.TotalMinor
            || request.Currency != "IRR" || request.Status != Refahi.Modules.Hotels.Domain.Aggregates.HotelRequestAgg.Enums.HotelRequestStatus.ConvertedToOrder
            || request.ExpireAt <= DateTime.UtcNow) return "رزرو هتل دیگر قابل پرداخت نیست.";
        var reservation = await catalogue.ReservationAsync(id, ct);
        if (request.ProviderName == "Lamasoo" && reservation is null) return "رزرو خارجی معتبر یافت نشد.";
        if (reservation is not null && (reservation.State != HotelExternalState.Reserved || reservation.PaymentBlocked
            || reservation.SupplyPriceMinor > context.TotalMinor || reservation.LockedUntilUtc <= DateTime.UtcNow.AddSeconds(lifecycle?.Value.PaymentSafetySeconds ?? 60)))
            return "اعتبار رزرو هتل به پایان رسیده یا وضعیت آن مشخص نیست.";
        return null;
    }
    public async Task<IAsyncDisposable> AcquireAsync(OrderPaymentContext context, CancellationToken ct)
    {
        var held = await catalogue.LockAsync("reservation:" + context.SourceReferenceId, ct);
        try { var reason = await GetUnavailableReasonAsync(context, ct); if (reason is not null) throw new InvalidOperationException(reason); return held; }
        catch { await held.DisposeAsync(); throw; }
    }
}
