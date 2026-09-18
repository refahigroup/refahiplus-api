using System.Reflection;
using System.Text.Json;
using Refahi.Modules.CreditPayments.Application.Contracts.IntegrationEvents;

namespace Refahi.Modules.CreditPayments.Tests;

public sealed class Phase13ContractTests
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private static readonly DateTimeOffset OccurredAt = new(2026, 9, 18, 4, 30, 0, TimeSpan.Zero);

    [Fact]
    public void Captured_v1_has_frozen_golden_json()
    {
        var value = new CreditPaymentCapturedV1(
            Guid.Parse("00000000-0000-0000-0000-000000000001"),
            CreditPaymentSettlementEventTypes.CapturedV1, 1, OccurredAt,
            CreditPaymentSettlementEventTypes.Source,
            Guid.Parse("00000000-0000-0000-0000-000000000002"),
            Guid.Parse("00000000-0000-0000-0000-000000000003"),
            Guid.Parse("00000000-0000-0000-0000-000000000004"),
            Guid.Parse("00000000-0000-0000-0000-000000000005"), "merchant-order-1",
            Guid.Parse("00000000-0000-0000-0000-000000000006"), 10_000_000, 10_000_000,
            "IRR", Guid.Parse("00000000-0000-0000-0000-000000000007"), "store.clothing",
            "policy-1", "7", new string('a', 64));

        Assert.Equal("{\"eventId\":\"00000000-0000-0000-0000-000000000001\",\"eventType\":\"credit-payment-captured.v1\",\"eventVersion\":1,\"occurredAt\":\"2026-09-18T04:30:00+00:00\",\"source\":\"CreditPayments\",\"gatewayPaymentId\":\"00000000-0000-0000-0000-000000000002\",\"merchantId\":\"00000000-0000-0000-0000-000000000003\",\"terminalId\":\"00000000-0000-0000-0000-000000000004\",\"supplierId\":\"00000000-0000-0000-0000-000000000005\",\"merchantOrderId\":\"merchant-order-1\",\"orderId\":\"00000000-0000-0000-0000-000000000006\",\"amountMinor\":10000000,\"capturedAmountMinor\":10000000,\"currency\":\"IRR\",\"walletPaymentId\":\"00000000-0000-0000-0000-000000000007\",\"approvedCategoryCode\":\"store.clothing\",\"merchantPaymentPolicyId\":\"policy-1\",\"merchantPaymentPolicyVersion\":\"7\",\"merchantPaymentPolicyHash\":\"aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa\"}",
            JsonSerializer.Serialize(value, Json));
    }

    [Fact]
    public void Refunded_v1_has_frozen_golden_json()
    {
        var value = new CreditPaymentRefundedV1(
            Guid.Parse("00000000-0000-0000-0000-000000000011"),
            CreditPaymentSettlementEventTypes.RefundedV1, 1, OccurredAt,
            CreditPaymentSettlementEventTypes.Source,
            Guid.Parse("00000000-0000-0000-0000-000000000012"),
            Guid.Parse("00000000-0000-0000-0000-000000000013"),
            Guid.Parse("00000000-0000-0000-0000-000000000014"),
            Guid.Parse("00000000-0000-0000-0000-000000000015"),
            Guid.Parse("00000000-0000-0000-0000-000000000016"),
            Guid.Parse("00000000-0000-0000-0000-000000000017"),
            Guid.Parse("00000000-0000-0000-0000-000000000018"),
            Guid.Parse("00000000-0000-0000-0000-000000000019"), 2_000_000, "IRR",
            10_000_000, 5_000_000, false, "refund-2");

        Assert.Equal("{\"eventId\":\"00000000-0000-0000-0000-000000000011\",\"eventType\":\"credit-payment-refunded.v1\",\"eventVersion\":1,\"occurredAt\":\"2026-09-18T04:30:00+00:00\",\"source\":\"CreditPayments\",\"refundId\":\"00000000-0000-0000-0000-000000000012\",\"gatewayPaymentId\":\"00000000-0000-0000-0000-000000000013\",\"merchantId\":\"00000000-0000-0000-0000-000000000014\",\"terminalId\":\"00000000-0000-0000-0000-000000000015\",\"supplierId\":\"00000000-0000-0000-0000-000000000016\",\"orderId\":\"00000000-0000-0000-0000-000000000017\",\"walletPaymentId\":\"00000000-0000-0000-0000-000000000018\",\"walletRefundId\":\"00000000-0000-0000-0000-000000000019\",\"refundAmountMinor\":2000000,\"currency\":\"IRR\",\"capturedAmountMinor\":10000000,\"cumulativeRefundedAmountMinor\":5000000,\"isFullyRefunded\":false,\"merchantRefundReference\":\"refund-2\"}",
            JsonSerializer.Serialize(value, Json));
    }

    [Fact]
    public void V1_contracts_exclude_customer_pii_and_settlement_calculations()
    {
        var forbidden = new[] { "CustomerId", "Mobile", "Email", "Username", "NationalId", "Otp",
            "CheckoutToken", "CommissionAmount", "VatAmount", "MerchantNetAmount", "SettlementAmount",
            "PayoutAmount", "ClawbackAmount" };
        var names = new[] { typeof(CreditPaymentCapturedV1), typeof(CreditPaymentRefundedV1) }
            .SelectMany(x => x.GetProperties(BindingFlags.Instance | BindingFlags.Public))
            .Select(x => x.Name).ToArray();
        Assert.DoesNotContain(names, x => forbidden.Contains(x, StringComparer.OrdinalIgnoreCase));
    }
}
