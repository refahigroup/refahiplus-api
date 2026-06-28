using Refahi.Modules.Store.Domain.Aggregates;
using Xunit;

namespace Refahi.Modules.Store.Tests;

public sealed class CartSnapshotPricingTests
{
    [Fact]
    public void AddItem_PreservesOriginalUnitPriceSnapshot_WhenSameItemIsMerged()
    {
        var userId = Guid.Parse("10000000-0000-0000-0000-000000000001");
        var shopId = Guid.Parse("20000000-0000-0000-0000-000000000001");
        var productId = Guid.Parse("30000000-0000-0000-0000-000000000001");
        var variantId = Guid.Parse("40000000-0000-0000-0000-000000000001");
        var cart = Cart.Create(userId, moduleId: 1);

        cart.AddItem(shopId, productId, variantId, sessionId: null, usageDate: null, quantity: 1, unitPriceMinor: 1700);
        cart.AddItem(shopId, productId, variantId, sessionId: null, usageDate: null, quantity: 2, unitPriceMinor: 1900);

        var item = Assert.Single(cart.Items);
        Assert.Equal(3, item.Quantity);
        Assert.Equal(1700, item.UnitPriceMinor);
        Assert.Equal(5100, cart.TotalMinor);
    }

    [Fact]
    public void UpdateItemQuantity_DoesNotChangeUnitPriceSnapshot()
    {
        var userId = Guid.Parse("10000000-0000-0000-0000-000000000002");
        var shopId = Guid.Parse("20000000-0000-0000-0000-000000000002");
        var productId = Guid.Parse("30000000-0000-0000-0000-000000000002");
        var cart = Cart.Create(userId, moduleId: 1);
        cart.AddItem(shopId, productId, variantId: null, sessionId: null, usageDate: null, quantity: 1, unitPriceMinor: 2500);
        var itemId = cart.Items.Single().Id;

        cart.UpdateItemQuantity(itemId, newQuantity: 4);

        var item = Assert.Single(cart.Items);
        Assert.Equal(4, item.Quantity);
        Assert.Equal(2500, item.UnitPriceMinor);
        Assert.Equal(10000, cart.TotalMinor);
    }
}
