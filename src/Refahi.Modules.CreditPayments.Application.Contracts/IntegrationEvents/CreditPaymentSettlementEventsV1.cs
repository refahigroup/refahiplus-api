using System.Text.Json.Serialization;
using Refahi.Shared.Domain;

namespace Refahi.Modules.CreditPayments.Application.Contracts.IntegrationEvents;

public static class CreditPaymentSettlementEventTypes
{
    public const string CapturedV1 = "credit-payment-captured.v1";
    public const string RefundedV1 = "credit-payment-refunded.v1";
    public const string Source = "CreditPayments";
    public const int Version1 = 1;
}

public sealed record CreditPaymentCapturedV1(
    [property: JsonPropertyName("eventId")] Guid EventId,
    [property: JsonPropertyName("eventType")] string EventType,
    [property: JsonPropertyName("eventVersion")] int EventVersion,
    [property: JsonPropertyName("occurredAt")] DateTimeOffset OccurredAt,
    [property: JsonPropertyName("source")] string Source,
    [property: JsonPropertyName("gatewayPaymentId")] Guid GatewayPaymentId,
    [property: JsonPropertyName("merchantId")] Guid MerchantId,
    [property: JsonPropertyName("terminalId")] Guid TerminalId,
    [property: JsonPropertyName("supplierId")] Guid? SupplierId,
    [property: JsonPropertyName("merchantOrderId")] string MerchantOrderId,
    [property: JsonPropertyName("orderId")] Guid OrderId,
    [property: JsonPropertyName("amountMinor")] long AmountMinor,
    [property: JsonPropertyName("capturedAmountMinor")] long CapturedAmountMinor,
    [property: JsonPropertyName("currency")] string Currency,
    [property: JsonPropertyName("walletPaymentId")] Guid WalletPaymentId,
    [property: JsonPropertyName("approvedCategoryCode")] string ApprovedCategoryCode,
    [property: JsonPropertyName("merchantPaymentPolicyId")] string MerchantPaymentPolicyId,
    [property: JsonPropertyName("merchantPaymentPolicyVersion")] string MerchantPaymentPolicyVersion,
    [property: JsonPropertyName("merchantPaymentPolicyHash")] string MerchantPaymentPolicyHash)
    : IIntegrationEvent;

public sealed record CreditPaymentRefundedV1(
    [property: JsonPropertyName("eventId")] Guid EventId,
    [property: JsonPropertyName("eventType")] string EventType,
    [property: JsonPropertyName("eventVersion")] int EventVersion,
    [property: JsonPropertyName("occurredAt")] DateTimeOffset OccurredAt,
    [property: JsonPropertyName("source")] string Source,
    [property: JsonPropertyName("refundId")] Guid RefundId,
    [property: JsonPropertyName("gatewayPaymentId")] Guid GatewayPaymentId,
    [property: JsonPropertyName("merchantId")] Guid MerchantId,
    [property: JsonPropertyName("terminalId")] Guid TerminalId,
    [property: JsonPropertyName("supplierId")] Guid? SupplierId,
    [property: JsonPropertyName("orderId")] Guid OrderId,
    [property: JsonPropertyName("walletPaymentId")] Guid WalletPaymentId,
    [property: JsonPropertyName("walletRefundId")] Guid WalletRefundId,
    [property: JsonPropertyName("refundAmountMinor")] long RefundAmountMinor,
    [property: JsonPropertyName("currency")] string Currency,
    [property: JsonPropertyName("capturedAmountMinor")] long CapturedAmountMinor,
    [property: JsonPropertyName("cumulativeRefundedAmountMinor")] long CumulativeRefundedAmountMinor,
    [property: JsonPropertyName("isFullyRefunded")] bool IsFullyRefunded,
    [property: JsonPropertyName("merchantRefundReference")] string? MerchantRefundReference)
    : IIntegrationEvent;

public sealed record CreditPaymentIntegrationEventMessage(
    Guid EventId,
    string EventType,
    int EventVersion,
    DateTimeOffset OccurredAt,
    string Source,
    string PayloadJson);

public interface ICreditPaymentIntegrationEventPublisher
{
    Task PublishAsync(CreditPaymentIntegrationEventMessage message, CancellationToken cancellationToken);
}

public interface ICreditPaymentIntegrationEventReconciler
{
    Task<int> ReconcileAsync(int batchSize, CancellationToken cancellationToken);
}

public sealed class SettlementIntegrationOptions
{
    public const string SectionName = "CreditPayments:SettlementIntegration";
    public bool PublicationEnabled { get; set; }
    public bool HistoricalReconciliationEnabled { get; set; } = true;
    public int PublicationPollSeconds { get; set; } = 10;
    public int ReconciliationPollSeconds { get; set; } = 60;
    public int BatchSize { get; set; } = 50;
    public int LeaseSeconds { get; set; } = 60;
    public int BaseRetrySeconds { get; set; } = 10;
    public int MaximumRetrySeconds { get; set; } = 3600;
}
