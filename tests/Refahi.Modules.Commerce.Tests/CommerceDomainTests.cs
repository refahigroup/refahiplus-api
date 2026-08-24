using Refahi.Modules.Commerce.Domain;
using Xunit;

namespace Refahi.Modules.Commerce.Tests;

public sealed class CommerceDomainTests
{
    [Fact]
    public void Order_groups_items_by_provider_and_tracks_one_total()
    {
        var order = CommerceOrder.Create(Guid.NewGuid(), "key", "hash", "protected-name", "protected-mobile",
        [
            Item("aabsar", "adult", 2, 100),
            Item("aabsar", "child", 1, 50),
            Item("future", "adult", 1, 200)
        ]);

        Assert.Equal(2, order.Fulfillments.Count);
        Assert.Equal(450, order.TotalAmountMinor);
        Assert.Equal(CommerceOrderStatus.PendingPayment, order.Status);
    }

    [Fact]
    public void Idempotency_rejects_a_different_payload()
    {
        var order = CommerceOrder.Create(Guid.NewGuid(), "key", "first", "n", "m", [Item("aabsar", "adult", 1, 100)]);
        var error = Assert.Throws<CommerceDomainException>(() => order.EnsureFingerprint("second"));
        Assert.Equal("IDEMPOTENCY_PAYLOAD_MISMATCH", error.ErrorCode);
    }

    [Fact]
    public void Paid_order_moves_through_fulfillment_and_manual_review()
    {
        var order = CommerceOrder.Create(Guid.NewGuid(), "key", "hash", "n", "m", [Item("aabsar", "adult", 1, 100)]);
        order.QueueFulfillment();
        order.BeginFulfillment();
        order.RequireManualReview();
        Assert.Equal(CommerceOrderStatus.ManualReview, order.Status);
    }

    [Fact]
    public void Cart_owns_items_and_rejects_invalid_quantity()
    {
        var cart = CommerceCart.Create(Guid.NewGuid());
        cart.AddOrReplace(new("aabsar", "aabsar", "event", "showtime", "adult", "title", "offer", 1, 100));
        var error = Assert.Throws<CommerceDomainException>(() => cart.UpdateQuantity(cart.Items.Single().Id, 0));
        Assert.Equal("INVALID_QUANTITY", error.ErrorCode);
    }

    private static CommerceOrderItemSnapshot Item(string provider, string option, int quantity, long price) =>
        new(provider, provider, $"event-{provider}", $"show-{provider}", option, "title", "offer",
            "entertainment.waterpark", quantity, price, "{}");
}
