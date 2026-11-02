using Refahi.Modules.Hotels.Domain.Abstraction.Repositories;
using Refahi.Modules.Hotels.Domain.Aggregates.Catalogue;
using Refahi.Modules.Orders.Application.Contracts.Cancellation;

namespace Refahi.Modules.Hotels.Application.Features.Catalogue;

public sealed class HotelCancellationParticipant(IHotelCatalogueRepository catalogue, IHotelBookingSagaRepository sagas, IHotelProviderBookingCacheRepository cache) : IOrderCancellationParticipant
{
    public string SourceModule => "Hotel";
    public async Task<OrderCancellationPreparation> PrepareAsync(OrderCancellationContext context, CancellationToken ct)
    {
        if (context.SourceReferenceId is not Guid id) return new(false, "مرجع رزرو موجود نیست.");
        await using var held = await catalogue.LockAsync("reservation:" + id, ct);
        var external = await catalogue.ReservationAsync(id, ct);
        if (external is null)
        {
            var saga = await sagas.GetByHotelRequestIdAsync(id, ct);
            var prior = saga is null ? null : await cache.GetBySagaIdAsync(saga.SagaId, ct);
            if (prior?.Status == Refahi.Modules.Hotels.Domain.Aggregates.ProviderBookingCacheAgg.Enums.ProviderBookingCacheStatus.ExternallyUnresolved)
                return new(false, "نتیجه رزرو خارجی مشخص نیست؛ بازگشت وجه نیاز به پیگیری دارد.");
            return new(true);
        }
        if (external.NoExternalLiability && external.State is HotelExternalState.Failed or HotelExternalState.Cancelled) return new(true);
        if (context.PaymentState == "Paid" || external.PaymentBlocked || external.State is HotelExternalState.Submitting or HotelExternalState.Confirming or HotelExternalState.Unknown or HotelExternalState.Confirmed)
            return new(false, "لغو و بازگشت وجه این رزرو نیاز به بررسی وضعیت تأمین‌کننده دارد.");
        return new(true);
    }
}
