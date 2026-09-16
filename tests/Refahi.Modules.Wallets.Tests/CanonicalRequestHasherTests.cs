using Refahi.Modules.Wallets.Application.Contracts.Infrastructure;
using Refahi.Modules.Wallets.Infrastructure.Persistence.Atomic;
using Xunit;

namespace Refahi.Modules.Wallets.Tests;

public sealed class CanonicalRequestHasherTests
{
    [Fact]
    public void PaymentIntent_IgnoresNonSemanticCollectionAndJsonPropertyOrder()
    {
        var walletA = Guid.Parse("00000000-0000-0000-0000-000000000001");
        var walletB = Guid.Parse("00000000-0000-0000-0000-000000000002");
        var orderId = Guid.NewGuid();
        var payerId = Guid.NewGuid();

        var first = CanonicalRequestHasher.HashPaymentIntent(
            orderId, 10, "IRR", payerId, null,
            [new(walletA, 3), new(walletB, 7)],
            [new(walletA, 1, 10, "sale")],
            ["Store.Clothing", "store"],
            "{\"b\":2.0e0,\"a\":1.00}"
        );
        var second = CanonicalRequestHasher.HashPaymentIntent(
            orderId, 10, "irr", payerId, null,
            [new(walletB, 7), new(walletA, 3)],
            [new(walletA, 1, 10, "sale")],
            ["store", "store.clothing"],
            "{\"a\":1,\"b\":2}"
        );

        Assert.Equal(first, second);
    }

    [Theory]
    [InlineData("amount")]
    [InlineData("payer")]
    [InlineData("currency")]
    [InlineData("destination")]
    [InlineData("allocation")]
    [InlineData("posting")]
    [InlineData("category")]
    public void PaymentIntent_FinancialDifferencesChangeHash(string difference)
    {
        var wallet = Guid.Parse("00000000-0000-0000-0000-000000000001");
        var other = Guid.Parse("00000000-0000-0000-0000-000000000002");
        var order = Guid.NewGuid();
        var payer = Guid.NewGuid();
        var baseline = CanonicalRequestHasher.HashPaymentIntent(
            order, 10, "IRR", payer, null,
            [new(wallet, 10)], [], ["store"], "{\"a\":1}"
        );
        var changed = CanonicalRequestHasher.HashPaymentIntent(
            order,
            difference == "amount" ? 11 : 10,
            difference == "currency" ? "USD" : "IRR",
            difference == "payer" ? other : payer,
            difference == "destination" ? other : null,
            [new(wallet, difference == "allocation" ? 9 : 10)],
            difference == "posting" ? [new(other, 1, 10, "sale")] : [],
            [difference == "category" ? "hotel" : "store"],
            "{\"a\":1}"
        );

        Assert.NotEqual(baseline, changed);
    }

    [Fact]
    public void Refund_AmountReasonAndMetadataAreCanonicalInputs()
    {
        var payment = Guid.NewGuid();
        var first = CanonicalRequestHasher.HashRefund(payment, 2_000, "reason", "{\"b\":2,\"a\":1}");
        var equivalent = CanonicalRequestHasher.HashRefund(payment, 2_000, "reason", "{\"a\":1,\"b\":2}");
        var differentAmount = CanonicalRequestHasher.HashRefund(payment, 2_001, "reason", "{\"a\":1,\"b\":2}");

        Assert.Equal(first, equivalent);
        Assert.NotEqual(first, differentAmount);
    }
}
