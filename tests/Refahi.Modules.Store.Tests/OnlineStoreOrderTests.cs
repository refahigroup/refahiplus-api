using Microsoft.EntityFrameworkCore;
using Refahi.Modules.Store.Domain.Aggregates;
using Refahi.Modules.Store.Domain.Entities;
using Refahi.Modules.Store.Domain.Enums;
using Refahi.Modules.Store.Domain.Exceptions;
using Refahi.Modules.Store.Infrastructure.Persistence.Context;
using Xunit;

namespace Refahi.Modules.Store.Tests;

public sealed class OnlineStoreOrderCanonicalTests
{
    [Fact]
    public void Store_order_is_created_before_order_with_immutable_financial_snapshot()
    {
        var snapshot = Snapshot(final: 800, original: 1000, quantity: 2);
        var order = StoreOrder.Create(
            Guid.NewGuid(),
            7,
            snapshot.ShopId,
            snapshot.SupplierId,
            "checkout-1",
            new string('a', 64),
            [snapshot]
        );

        Assert.Equal(StoreOrderStatus.PendingOrder, order.Status);
        Assert.Null(order.OrderId);
        Assert.Equal(2000, order.OriginalAmountMinor);
        Assert.Equal(1600, order.FinalAmountMinor);
        Assert.Equal(400, order.DiscountAmountMinor);
        Assert.Equal(160, order.Items.Single().CommissionAmountMinor);

        order.AttachOrder(Guid.NewGuid());
        Assert.Equal(StoreOrderStatus.PendingPayment, order.Status);
        order.MarkPaid();
        order.MarkPaid(); // duplicate integration delivery is idempotent
        Assert.Equal(StoreOrderStatus.Paid, order.Status);
        Assert.Equal(800, order.Items.Single().FinalUnitPriceMinor);
    }

    [Fact]
    public void Snapshot_does_not_reference_mutable_offer_prices()
    {
        var now = DateTimeOffset.UtcNow;
        var offer = Offer.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            null,
            null,
            1000,
            20,
            now.AddMinutes(-1),
            null
        );
        var snapshot = Snapshot(offer.Id, offer.FinalPriceMinor, offer.OriginalPriceMinor, 1);
        var order = StoreOrder.Create(
            Guid.NewGuid(),
            1,
            snapshot.ShopId,
            snapshot.SupplierId,
            "snapshot",
            new string('b', 64),
            [snapshot]
        );

        offer.Update(2000, 0, now.AddMinutes(-1), null);

        Assert.Equal(800, order.Items.Single().FinalUnitPriceMinor);
        Assert.Equal(1000, order.Items.Single().OriginalUnitPriceMinor);
    }

    [Fact]
    public void Store_order_rejects_mixed_shop_items()
    {
        var first = Snapshot();
        var second = Snapshot() with { ShopId = Guid.NewGuid() };
        var ex = Assert.Throws<StoreDomainException>(() =>
            StoreOrder.Create(
                Guid.NewGuid(),
                1,
                first.ShopId,
                first.SupplierId,
                "mixed",
                new string('c', 64),
                [first, second]
            )
        );
        Assert.Equal("MIXED_SHOP_ITEMS", ex.ErrorCode);
    }

    [Fact]
    public void Lifecycle_rejects_refund_before_payment_and_different_order_attachment()
    {
        var item = Snapshot();
        var order = StoreOrder.Create(
            Guid.NewGuid(),
            1,
            item.ShopId,
            item.SupplierId,
            "lifecycle",
            new string('d', 64),
            [item]
        );
        Assert.Throws<StoreDomainException>(order.MarkRefunded);
        order.AttachOrder(Guid.NewGuid());
        Assert.Throws<StoreDomainException>(() => order.AttachOrder(Guid.NewGuid()));
    }

    [Fact]
    public void Ef_model_has_idempotency_order_uniqueness_and_xmin_concurrency()
    {
        using var db = new StoreDbContext(
            new DbContextOptionsBuilder<StoreDbContext>()
                .UseNpgsql("Host=localhost;Database=model;Username=model;Password=model")
                .Options
        );
        var entity = db.Model.FindEntityType(typeof(StoreOrder))!;
        Assert.True(
            entity
                .GetIndexes()
                .Single(x =>
                    x.Properties.Select(p => p.Name)
                        .SequenceEqual([
                            nameof(StoreOrder.UserId),
                            nameof(StoreOrder.IdempotencyKey),
                        ])
                )
                .IsUnique
        );
        Assert.True(
            entity
                .GetIndexes()
                .Single(x =>
                    x.Properties.Count == 1 && x.Properties[0].Name == nameof(StoreOrder.OrderId)
                )
                .IsUnique
        );
        Assert.True(entity.FindProperty(nameof(StoreOrder.Version))!.IsConcurrencyToken);

        var cartItem = db.Model.FindEntityType(typeof(CartItem))!;
        Assert.NotNull(cartItem.FindProperty(nameof(CartItem.OfferId)));
        Assert.NotNull(cartItem.FindProperty(nameof(CartItem.OriginalUnitPriceMinor)));
    }

    [Fact]
    public void Online_checkout_handler_has_no_wallet_dependency()
    {
        var constructors =
            typeof(Refahi.Modules.Store.Application.Features.Checkout.PlaceStoreOrder.PlaceStoreOrderCommandHandler)
                .GetConstructors()
                .SelectMany(x => x.GetParameters())
                .Select(x => x.ParameterType.FullName)
                .ToArray();
        Assert.DoesNotContain(
            constructors,
            x => x?.Contains("Wallet", StringComparison.OrdinalIgnoreCase) == true
        );
    }

    private static StoreOrderItemSnapshot Snapshot(
        Guid? offerId = null,
        long final = 800,
        long original = 1000,
        int quantity = 2
    ) =>
        new(
            Guid.NewGuid(),
            Guid.NewGuid(),
            null,
            null,
            offerId ?? Guid.NewGuid(),
            "محصول",
            null,
            null,
            11,
            "store.test",
            Guid.NewGuid(),
            Guid.NewGuid(),
            SalesChannel.Online,
            ProductType.Goods,
            SalesModel.Unlimited,
            FulfillmentMethod.Download,
            quantity,
            original,
            20,
            final,
            Guid.NewGuid(),
            Guid.NewGuid(),
            10,
            null
        );
}
