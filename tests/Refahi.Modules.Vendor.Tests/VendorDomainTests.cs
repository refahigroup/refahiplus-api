using Refahi.Modules.Orders.Domain.Aggregates;
using Refahi.Modules.Orders.Domain.Enums;
using Refahi.Modules.Store.Domain.Aggregates;
using Refahi.Modules.Store.Domain.Enums;
using Refahi.Modules.Store.Domain.Exceptions;
using Xunit;

namespace Refahi.Modules.Vendor.Tests;

public sealed class VendorDomainTests
{
    [Fact]
    public void InPersonOrderKeepsScopeAndNeedsNoSourceReference()
    {
        var vendorId = Guid.NewGuid();
        var shopId = Guid.NewGuid();
        var operatorId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var order = Order.Create(
            Guid.NewGuid(),
            "Store",
            null,
            "vendor-scope-test",
            "StoreInPerson",
            [
                new OrderItemData(
                    "فروش حضوری",
                    1_000,
                    1,
                    0,
                    productId,
                    "store.in-person",
                    null,
                    null
                ),
            ],
            sourceOwnerId: vendorId,
            sourceShopId: shopId,
            createdByUserId: operatorId,
            financialSnapshot: new OrderFinancialSnapshotData(1_000, 0, 0, 0, 0, 1_000),
            paymentPostings:
            [
                new OrderPaymentPostingData(
                    Guid.NewGuid(),
                    PaymentPostingDirection.Credit,
                    1_000,
                    "store.vendor-gross"
                ),
            ]
        );

        Assert.Null(order.SourceReferenceId);
        Assert.Equal(productId, order.Items.Single().SourceItemId);
        Assert.Equal(vendorId, order.SourceOwnerId);
        Assert.Equal(shopId, order.SourceShopId);
        Assert.Equal(operatorId, order.CreatedByUserId);
    }

    [Fact]
    public void ShopTypeOnlyContainsOnlineAndInPersonChannels() =>
        Assert.Equal([ShopType.Online, ShopType.InPerson], Enum.GetValues<ShopType>().Distinct());

    [Fact]
    public void InPersonFinancialSnapshotAcceptsGrossCommissionVatAndNetPlan()
    {
        var vendorWallet = Guid.NewGuid();
        var revenueWallet = Guid.NewGuid();
        var vatWallet = Guid.NewGuid();
        var order = Order.Create(
            Guid.NewGuid(),
            "Store",
            null,
            "financial-plan-test",
            "StoreInPerson",
            [
                new OrderItemData(
                    "خدمت حضوری",
                    1_000_000,
                    1,
                    0,
                    Guid.NewGuid(),
                    "store.in-person",
                    null,
                    null
                ),
            ],
            sourceOwnerId: Guid.NewGuid(),
            sourceShopId: Guid.NewGuid(),
            createdByUserId: Guid.NewGuid(),
            financialSnapshot: new OrderFinancialSnapshotData(
                1_000_000,
                10,
                100_000,
                10,
                10_000,
                890_000
            ),
            paymentPostings:
            [
                new(vendorWallet, PaymentPostingDirection.Credit, 1_000_000, "store.vendor-gross"),
                new(vendorWallet, PaymentPostingDirection.Debit, 100_000, "store.commission"),
                new(
                    revenueWallet,
                    PaymentPostingDirection.Credit,
                    100_000,
                    "store.platform-revenue"
                ),
                new(vendorWallet, PaymentPostingDirection.Debit, 10_000, "store.vat"),
                new(vatWallet, PaymentPostingDirection.Credit, 10_000, "store.platform-vat"),
            ]
        );

        Assert.Equal(890_000, order.RecipientNetAmountMinor);
        Assert.Equal(5, order.PaymentPostings.Count);
    }

    [Fact]
    public void ShopChannelCannotBeChangedAfterCreation()
    {
        var shop = Shop.Create("فروشگاه تست", "vendor-shop-test", ShopType.Online, Guid.NewGuid());
        var ex = Assert.Throws<StoreDomainException>(() =>
            shop.UpdateInfo(
                shop.Name,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                ShopType.InPerson
            )
        );
        Assert.Equal("SHOP_CHANNEL_IMMUTABLE", ex.ErrorCode);
        Assert.Equal(ShopType.Online, shop.ShopType);
    }
}
