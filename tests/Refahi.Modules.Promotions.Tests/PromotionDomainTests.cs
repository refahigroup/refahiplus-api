using Refahi.Modules.Promotions.Domain;
using Xunit;
namespace Refahi.Modules.Promotions.Tests;

public sealed class PromotionDomainTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 17, 12, 0, 0, TimeSpan.Zero);
    private static PromotionCampaign Campaign(DiscountKind kind = DiscountKind.Fixed, long fixedAmount = 3, decimal percent = 10, long max = 100)
    {
        var c = PromotionCampaign.Create("تست", kind, fixedAmount, percent, max, 0, 10000, 100, 1, [], [], [], [], Now.AddDays(-1), Now.AddDays(1));
        c.SetActive(true, Now); return c;
    }
    [Fact]
    public void LargestRemainder_IsExactAndDeterministic()
    {
        var a = Guid.Parse("00000000-0000-0000-0000-000000000001"); var b = Guid.Parse("00000000-0000-0000-0000-000000000002");
        var quote = Campaign().Calculate(Guid.NewGuid(), "Store", [], [new(a, "store", 5), new(b, "store", 5)], 10, Now);
        Assert.Equal(3, quote.Shares.Sum(x => x.AmountMinor)); Assert.Equal(2, quote.Shares.Single(x => x.OrderItemId == a).AmountMinor);
        Assert.Equal(quote.Shares, Campaign().Calculate(Guid.NewGuid(), "Store", [], [new(b, "store", 5), new(a, "store", 5)], 10, Now).Shares);
    }
    [Fact]
    public void LargeRialAmounts_AllocateWithoutIntermediateOverflow()
    {
        var discount = long.MaxValue / 3; var c = Campaign(fixedAmount: discount, max: discount);
        var quote = c.Calculate(Guid.NewGuid(), "Store", [], [new(Guid.NewGuid(), "store", long.MaxValue / 2), new(Guid.NewGuid(), "store", long.MaxValue / 2)], long.MaxValue - 1, Now);
        Assert.Equal(discount, quote.Shares.Sum(x => x.AmountMinor)); Assert.Equal(2, quote.Shares.Length);
    }
    [Fact]
    public void Percentage_UsesIntegerRialRoundingAndCap()
    {
        var c = Campaign(DiscountKind.Percentage, 0, 10, 2);
        Assert.Equal(2, c.Calculate(Guid.NewGuid(), "Store", [], [new(Guid.NewGuid(), "store", 25)], 25, Now).DiscountMinor);
    }
    [Fact]
    public void Shipping_DoesNotIncreaseEligibleBasis()
    {
        var c = Campaign(DiscountKind.Percentage, 0, 10);
        Assert.Equal(1, c.Calculate(Guid.NewGuid(), "Store", [], [new(Guid.NewGuid(), "store", 10)], 1000, Now).DiscountMinor);
    }
    [Fact]
    public void ZeroCustomerPayment_IsRejected()
    { Assert.Throws<PromotionRuleException>(() => Campaign(fixedAmount: 10).Calculate(Guid.NewGuid(), "Store", [], [new(Guid.NewGuid(), "store", 10)], 10, Now)); }
    [Fact]
    public void OrganizationAndCategory_AreBothRequired()
    {
        var org = Guid.NewGuid(); var c = PromotionCampaign.Create("تست", DiscountKind.Fixed, 2, 0, 2, 5, 1000, 100, 1, ["Store"], ["store.clothing"], [org], [], Now.AddDays(-1), Now.AddDays(1)); c.SetActive(true, Now);
        var user = Guid.NewGuid(); var lines = new[] { new PromotionLine(Guid.NewGuid(), "store.clothing.shirt", 10), new PromotionLine(Guid.NewGuid(), "store.food", 100) };
        Assert.Throws<PromotionRuleException>(() => c.Calculate(user, "Store", [], lines, 110, Now));
        var result = c.Calculate(user, "Store", [org], lines, 110, Now);
        Assert.Equal(org, result.OrganizationId); Assert.Single(result.Shares); Assert.Equal(lines[0].Id, result.Shares[0].OrderItemId);
    }
    [Fact]
    public void LatePaidEvent_DoesNotUndoRefund()
    {
        var r = PromotionRedemption.Reserve(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 1, "key", "hash", new(2, null, []), Now.AddMinutes(5), Now);
        r.BeginPayment(Now); r.Reverse(); r.Commit(Guid.NewGuid()); Assert.Equal(RedemptionStatus.Reversed, r.Status);
    }
    [Fact]
    public void ActiveCampaignRules_CannotChange()
    {
        var c = Campaign(); Assert.Throws<PromotionRuleException>(() => c.Update("تغییر", DiscountKind.Fixed, 1, 0, 10, 0, 100, 1, 1, [], [], [], [], Now, Now.AddDays(1)));
    }
}
