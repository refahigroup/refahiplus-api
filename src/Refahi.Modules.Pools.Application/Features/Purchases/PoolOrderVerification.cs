using Refahi.Modules.Orders.Application.Contracts.Dtos;
using Refahi.Modules.Pools.Domain;
namespace Refahi.Modules.Pools.Application.Features.Purchases;
internal static class PoolOrderVerification
{
    public static bool Matches(PoolPurchase p, OrderDto? o) => o != null && o.Id == p.OrderId && o.UserId == p.UserId && o.SourceModule == "Pools"
        && o.ReferenceType == "PoolPurchase" && o.SourceReferenceId == p.Id && o.SourceAmountMinor == p.TotalMinor && o.Currency == "IRR"
        && o.PayableUntil == p.PayableUntil && o.Items.Count == p.Requests.Count && o.Items.Select(x=>x.SourceItemId).Distinct().Count() == p.Requests.Count
        && o.Items.All(item=>p.Requests.Any(r=>r.Id==item.SourceItemId && item.Quantity==r.OrderQuantity && item.UnitPriceMinor==r.OrderUnitPrice
            && item.FinalPriceMinor==r.TotalMinor && item.CategoryCode==PoolKeys.Category && item.DeliveryMethod==0));
}
