using Refahi.Modules.Wallets.Domain.Aggregates;
using Refahi.Modules.Wallets.Domain.ValueObjects;
using Xunit;

namespace Refahi.Modules.Wallets.Tests;

public sealed class WalletAccountingPolicyTests
{
    [Theory]
    [InlineData("store", "store", true)]
    [InlineData("store.clothing", "store", true)]
    [InlineData("store", "store.clothing", false)]
    [InlineData("storefront", "store", false)]
    [InlineData("STORE.Clothing", "store", true)]
    [InlineData("", "store", false)]
    public void CategoryMatching_IsTaxonomyAware(
        string requested,
        string allowed,
        bool expected
    ) => Assert.Equal(expected, Wallet.IsCategoryWithin(requested, allowed));

    [Fact]
    public void BalanceEffects_DescribeEverySupportedProjectionMutation()
    {
        Assert.Equal(new BalanceEffect(100, 0), BalanceEffect.TopUp(100));
        Assert.Equal(new BalanceEffect(-100, 100), BalanceEffect.Reserve(100));
        Assert.Equal(new BalanceEffect(0, -100), BalanceEffect.Capture(100));
        Assert.Equal(new BalanceEffect(100, -100), BalanceEffect.Release(100));
        Assert.Equal(new BalanceEffect(100, 0), BalanceEffect.AvailableCredit(100));
        Assert.Equal(new BalanceEffect(-100, 0), BalanceEffect.AvailableDebit(100));
    }
}
