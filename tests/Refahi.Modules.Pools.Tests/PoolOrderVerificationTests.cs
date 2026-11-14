using Refahi.Modules.Orders.Application.Contracts.Dtos;
using Refahi.Modules.Pools.Application.Features.Purchases;
using Refahi.Modules.Pools.Domain;
using Xunit;

namespace Refahi.Modules.Pools.Tests;

public sealed class PoolOrderVerificationTests
{
    [Fact]
    public void Promotion_does_not_change_the_verified_source_price()
    {
        var now = DateTimeOffset.UtcNow;
        var request = PoolRequest.Create(Guid.NewGuid(), Guid.NewGuid(), "poolticket", "12", "9", "{}",
            "استخر", "2026-11-04", "10:00 - 12:00", 1, 0, 1000, now.AddDays(1), now.AddDays(1).AddHours(2));
        var purchase = PoolPurchase.Create(Guid.NewGuid(), "key", "hash", [request], now, now.AddMinutes(10));
        purchase.AttachOrder(Guid.NewGuid(), "R-1");
        var item = new OrderItemDto(Guid.NewGuid(), "استخر", 1000, 1, 1000, request.Id,
            "entertainment.waterpark", null, null, 0);
        var order = new OrderDto(purchase.OrderId!.Value, "R-1", purchase.UserId, 1000, 0, 0, "PROMO", 200,
            800, "Paid", "Paid", "Pools", purchase.Id, "PoolPurchase", null, null, null, 0, [item], now,
            PayableUntil: purchase.PayableUntil, SourceAmountMinor: 1000, AmountBeforePromotionMinor: 1000, PricingVersion: 2);
        Assert.True(PoolOrderVerification.Matches(purchase, order));
        Assert.False(PoolOrderVerification.Matches(purchase, order with { SourceAmountMinor = 800 }));
        Assert.False(PoolOrderVerification.Matches(purchase, order with { UserId = Guid.NewGuid() }));
        Assert.False(PoolOrderVerification.Matches(purchase, order with { Items = [item with { UnitPriceMinor = 800 }] }));
    }
}
