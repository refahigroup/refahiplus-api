using System.Text.Json;
using Refahi.Modules.CreditPayments.Domain;
using Refahi.Modules.CreditPayments.Infrastructure.Persistence;

namespace Refahi.Modules.CreditPayments.Infrastructure;

internal static class WebhookEventFactory
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public static WebhookEvent CreatePayment(GatewayPayment payment, string eventType,
        DateTimeOffset occurredAt, DateTimeOffset createdAt)
    {
        var id = Guid.NewGuid();
        var publicStatus = eventType["payment.".Length..];
        object data = eventType == WebhookEventTypes.PaymentSucceeded
            ? new
            {
                paymentId = payment.Id,
                merchantOrderId = payment.MerchantOrderId,
                status = publicStatus,
                amountMinor = payment.AmountMinor,
                currency = payment.Currency,
                capturedAmountMinor = payment.CapturedAmountMinor,
                paidAt = payment.PaidAt
            }
            : new
            {
                paymentId = payment.Id,
                merchantOrderId = payment.MerchantOrderId,
                status = publicStatus,
                amountMinor = payment.AmountMinor,
                currency = payment.Currency
            };
        var payload = JsonSerializer.Serialize(new
        {
            eventId = id,
            type = eventType,
            version = WebhookEvent.CurrentVersion,
            occurredAt,
            merchantId = payment.MerchantId,
            terminalId = payment.TerminalId,
            data
        }, Json);
        return WebhookEvent.Create(id, $"{eventType}:{payment.Id:N}", eventType,
            payment.MerchantId, payment.TerminalId, "GatewayPayment", payment.Id,
            occurredAt, payload, createdAt);
    }

    public static WebhookEvent CreateRefund(Refund refund, GatewayPayment payment,
        string eventType, DateTimeOffset occurredAt, DateTimeOffset createdAt)
    {
        var id = Guid.NewGuid();
        var publicStatus = eventType == WebhookEventTypes.RefundSucceeded ? "succeeded" : "failed";
        var payload = JsonSerializer.Serialize(new
        {
            eventId = id,
            type = eventType,
            version = WebhookEvent.CurrentVersion,
            occurredAt,
            merchantId = payment.MerchantId,
            terminalId = payment.TerminalId,
            data = new
            {
                refundId = refund.Id,
                paymentId = payment.Id,
                merchantRefundReference = refund.MerchantRefundReference,
                status = publicStatus,
                amountMinor = refund.AmountMinor,
                currency = refund.Currency,
                completedAt = refund.CompletedAt,
                failureCode = eventType == WebhookEventTypes.RefundFailed
                    ? refund.NormalizedFailureCode : null
            }
        }, Json);
        return WebhookEvent.Create(id, $"{eventType}:{refund.Id:N}", eventType,
            payment.MerchantId, payment.TerminalId, "Refund", refund.Id,
            occurredAt, payload, createdAt);
    }

    public static void Add(CreditPaymentsDbContext db, WebhookEvent webhookEvent, DateTimeOffset now)
    {
        db.WebhookEvents.Add(webhookEvent);
        db.OutboxMessages.Add(OutboxMessage.Create(webhookEvent.Id, now));
        db.AuditRecords.Add(AuditRecord.Create("webhook-event.created", "WebhookEvent",
            webhookEvent.Id, null, now));
    }

    public static void AddPayment(CreditPaymentsDbContext db, GatewayPayment payment,
        string eventType, DateTimeOffset occurredAt, DateTimeOffset now) =>
        Add(db, CreatePayment(payment, eventType, occurredAt, now), now);

    public static void AddRefund(CreditPaymentsDbContext db, Refund refund, GatewayPayment payment,
        string eventType, DateTimeOffset occurredAt, DateTimeOffset now) =>
        Add(db, CreateRefund(refund, payment, eventType, occurredAt, now), now);
}
