using Refahi.Modules.Orders.Application.Contracts.Cancellation;
using Refahi.Modules.Pools.Application.Contracts;
using Refahi.Modules.Pools.Application.Contracts.Public;
using Refahi.Modules.Pools.Domain;
namespace Refahi.Modules.Pools.Application.Features.Purchases.Cancel;
public sealed class PoolCancellationParticipant(IPoolMutationLock gate, PoolPurchaseAccess access, IPoolPurchaseRepository repository,
    ILegacyPoolStore legacy) : IOrderCancellationParticipant
{
    public string SourceModule => "Pools";
    public async Task<OrderCancellationPreparation> PrepareAsync(OrderCancellationContext context, CancellationToken ct)
    {
        if (context.SourceReferenceId is not Guid id) return new(false, "مرجع خرید موجود نیست");
        if (context.ReferenceType == "PoolBooking") return new(await legacy.IsLegacyAsync(id, context.UserId, ct), "درخواست قدیمی معتبر نیست");
        if (context.ReferenceType != "PoolPurchase") return new(false, "نوع مرجع خرید معتبر نیست");
        await using var lease = await gate.AcquireAsync(id, ct); var p = await access.Owned(id, context.UserId, ct);
        if (p.OrderId != context.OrderId) return new(false, "سفارش مالی مطابقت ندارد");
        if (p.Requests.Any(x => x.VoucherStatus == PoolVoucherStatus.Redeemed)) return new(false, "ووچر مصرف‌شده قابل استرداد نیست");
        p.BeginCancellation(); await repository.SaveAsync(ct); return new(true);
    }
}
