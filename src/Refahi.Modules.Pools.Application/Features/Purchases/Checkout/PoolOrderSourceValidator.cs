using MediatR;
using Microsoft.Extensions.Options;
using Refahi.Modules.Orders.Application.Contracts.Commands;
using Refahi.Modules.Pools.Application.Contracts;
namespace Refahi.Modules.Pools.Application.Features.Purchases.Checkout;
public sealed class PoolOrderSourceValidator(PoolPurchaseAccess access, IOptionsMonitor<PoolsOptions> options, TimeProvider clock) : INotificationHandler<ValidateOrderSourceNotification>
{
    public async Task Handle(ValidateOrderSourceNotification n, CancellationToken ct)
    {
        var r = n.Order;
        if (!r.SourceModule.Equals("Pools", StringComparison.OrdinalIgnoreCase) && r.ReferenceType is not ("PoolPurchase" or "PoolBooking")) return;
        if (!options.CurrentValue.PurchaseEnabled || r.SourceModule != "Pools" || r.ReferenceType != "PoolPurchase" || r.SourceReferenceId is not Guid id)
            throw new PoolException("مرجع سفارش استخر معتبر نیست");
        var p = await access.Owned(id, r.UserId, ct);
        if (!p.CanPay(clock.GetUtcNow()) || p.OrderId.HasValue || r.Items.Count != p.Requests.Count || r.Items.Select(x => x.SourceItemId).Distinct().Count() != p.Requests.Count
            || r.IdempotencyKey != PoolKeys.Order(id) || r.PayableUntil != p.PayableUntil || r.SagaId != p.Id
            || r.ShippingFeeMinor != 0 || r.DiscountCodeAmountMinor != 0 || r.ShippingAddressId != null || r.ShippingAddressSnapshotJson != null
            || r.DeliveryDate != null || r.DeliveryTimeSlot != 0 || r.DiscountCode != null || r.FinancialSnapshot != null
            || r.PaymentPostings?.Count > 0 || r.SourceOwnerId != null || r.SourceShopId != null || r.CreatedByUserId != null)
            throw new PoolException("سفارش مالی با خرید مطابقت ندارد", 409, "ORDER_SOURCE_MISMATCH");
        foreach (var item in r.Items)
        {
            var request = p.Requests.SingleOrDefault(x => x.Id == item.SourceItemId);
            if (request == null || item.Quantity != 1 || item.UnitPriceMinor != request.TotalMinor || item.DiscountAmountMinor != 0
                || item.CategoryCode != PoolKeys.Category || item.DeliveryMethod != 0)
                throw new PoolException("آیتم سفارش مالی معتبر نیست", 409, "ORDER_SOURCE_MISMATCH");
        }
    }
}
