using System.Reflection;
using Refahi.Modules.CreditPayments.Domain;

namespace Refahi.Modules.CreditPayments.Tests;

public sealed class Phase03GatewayPaymentTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 16, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Gateway_payment_starts_created_and_exposes_complete_stable_status_vocabulary()
    {
        var payment = GatewayPayment.Create(Guid.NewGuid(), Guid.NewGuid(), " INV-1 ", 100, "IRR", " test ",
            "{}", "https://merchant.example/callback", Now.AddMinutes(15), "پذیرنده", "وب", "store", Now);

        Assert.Equal(GatewayPaymentStatus.Created, payment.Status);
        Assert.Equal("INV-1", payment.MerchantOrderId);
        Assert.Equal("test", payment.Description);
        Assert.Equal(10, Enum.GetValues<GatewayPaymentStatus>().Length);
        Assert.Contains(GatewayPaymentStatus.AwaitingFunding, Enum.GetValues<GatewayPaymentStatus>());
        Assert.Null(typeof(GatewayPayment).GetMethod("SetStatus", BindingFlags.Public | BindingFlags.Instance));
        Assert.All(new[] { nameof(GatewayPayment.MerchantId), nameof(GatewayPayment.TerminalId),
            nameof(GatewayPayment.MerchantOrderId), nameof(GatewayPayment.AmountMinor), nameof(GatewayPayment.Currency),
            nameof(GatewayPayment.ReturnUrl), nameof(GatewayPayment.ApprovedCategoryCodeSnapshot) }, name =>
            Assert.False(typeof(GatewayPayment).GetProperty(name)!.SetMethod?.IsPublic ?? false));
    }

    [Theory]
    [InlineData("short")]
    [InlineData("has space")]
    [InlineData("کلید-غیر-ascii")]
    public void Invalid_idempotency_keys_are_rejected(string key) =>
        Assert.Throws<CreditPaymentsDomainException>(() => IdempotencyRecord.ValidateKey(key));
}
