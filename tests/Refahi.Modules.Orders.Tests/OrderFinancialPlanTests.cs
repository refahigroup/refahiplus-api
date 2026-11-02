using Refahi.Modules.Orders.Domain.Aggregates;
using Refahi.Modules.Orders.Domain.Enums;
using Refahi.Modules.Orders.Domain.Exceptions;

namespace Refahi.Modules.Orders.Tests;

public sealed class OrderFinancialPlanTests
{
    [Theory]
    [InlineData("StoreOrder", true)]
    [InlineData("storeorder", true)]
    [InlineData("StoreInPerson", false)]
    public void In_person_plan_preserves_source_and_financial_snapshot(string referenceType, bool hasSource)
    {
        Guid? source = hasSource ? Guid.NewGuid() : null;
        var order = Create(referenceType, source);

        Assert.Equal(source, order.SourceReferenceId);
        Assert.Equal(referenceType, order.ReferenceType);
        Assert.Equal(5_000_000, order.FinalAmountMinor);
        Assert.Equal(4_450_000, order.RecipientNetAmountMinor);
        Assert.Equal(3, order.PaymentPostings.Count);
    }

    [Fact]
    public void Online_store_order_does_not_require_a_financial_plan()
    {
        var order = Create("StoreOrder", Guid.NewGuid(), includeSnapshot: false, includePostings: false);

        Assert.Null(order.GrossAmountMinor);
        Assert.Empty(order.PaymentPostings);
    }

    [Theory]
    [InlineData("StoreOrder", false)]
    [InlineData("StoreInPerson", true)]
    public void In_person_source_reference_must_match_contract(string referenceType, bool hasSource)
    {
        var error = Assert.Throws<OrderDomainException>(() =>
            Create(referenceType, hasSource ? Guid.NewGuid() : null));

        Assert.Equal("INVALID_IN_PERSON_ORDER", error.ErrorCode);
    }

    [Fact]
    public void Unified_in_person_order_rejects_empty_source_reference()
    {
        var error = Assert.Throws<OrderDomainException>(() => Create("StoreOrder", Guid.Empty));

        Assert.Equal("INVALID_IN_PERSON_ORDER", error.ErrorCode);
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public void Unified_in_person_order_requires_both_snapshot_and_postings(bool snapshot, bool postings)
    {
        var error = Assert.Throws<OrderDomainException>(() =>
            Create("StoreOrder", Guid.NewGuid(), includeSnapshot: snapshot, includePostings: postings));

        Assert.Equal("IN_PERSON_FINANCIAL_PLAN_REQUIRED", error.ErrorCode);
    }

    [Theory]
    [InlineData(1, 1, 0, "INVALID_IN_PERSON_ORDER")]
    [InlineData(2, 0, 0, "INVALID_IN_PERSON_ORDER")]
    [InlineData(1, 0, 1, "UNBALANCED_PAYMENT_POSTINGS")]
    public void Unified_in_person_order_keeps_structure_and_balance_checks(
        int quantity, long shipping, long extraCredit, string code)
    {
        var error = Assert.Throws<OrderDomainException>(() =>
            Create("StoreOrder", Guid.NewGuid(), quantity: quantity, shipping: shipping, extraCredit: extraCredit));

        Assert.Equal(code, error.ErrorCode);
    }

    [Fact]
    public void Unified_in_person_order_rejects_incorrect_commission()
    {
        var error = Assert.Throws<OrderDomainException>(() =>
            Create("StoreOrder", Guid.NewGuid(), commission: 1));

        Assert.Equal("INVALID_IN_PERSON_FINANCIAL_SNAPSHOT", error.ErrorCode);
    }

    [Theory]
    [InlineData("Hotel", "HotelRequest")]
    [InlineData("Cinema", "StoreOrder")]
    [InlineData("Store", "Cart")]
    public void Unrelated_orders_cannot_attach_a_financial_plan(string module, string referenceType)
    {
        var error = Assert.Throws<OrderDomainException>(() =>
            Create(referenceType, Guid.NewGuid(), module: module));

        Assert.Equal("UNSUPPORTED_PAYMENT_PLAN", error.ErrorCode);
    }

    private static Order Create(
        string referenceType, Guid? source, bool includeSnapshot = true, bool includePostings = true,
        string module = "Store", int quantity = 1, long shipping = 0, long extraCredit = 0,
        long commission = 500_000) =>
        Order.Create(
            Guid.NewGuid(), module, source, Guid.NewGuid().ToString("N"), referenceType,
            [new OrderItemData("خرید حضوری", 5_000_000, quantity, 0, Guid.NewGuid(), "store", null, null)],
            shippingFeeMinor: shipping,
            financialSnapshot: includeSnapshot
                ? new OrderFinancialSnapshotData(5_000_000, 10, commission, 10, 50_000, 4_450_000)
                : null,
            paymentPostings: includePostings
                ? [
                    new(Guid.NewGuid(), PaymentPostingDirection.Credit, 4_450_000 + extraCredit, "vendor-net"),
                    new(Guid.NewGuid(), PaymentPostingDirection.Credit, 500_000, "commission"),
                    new(Guid.NewGuid(), PaymentPostingDirection.Credit, 50_000, "vat"),
                ]
                : null);
}
