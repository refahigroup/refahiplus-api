using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Refahi.Modules.CreditPayments.Application.Contracts.IntegrationEvents;
using Refahi.Modules.CreditPayments.Domain;
using Refahi.Modules.CreditPayments.Infrastructure.Persistence;

namespace Refahi.Modules.CreditPayments.Infrastructure;

internal static class SettlementIntegrationEventFactory
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public static string CaptureSource(Guid paymentId) => $"credit-payment-captured:{paymentId:N}";
    public static string RefundSource(Guid refundId) => $"credit-payment-refunded:{refundId:N}";

    public static CreditPaymentIntegrationEvent Captured(GatewayPayment payment, Guid supplierId,
        DateTimeOffset createdAt)
    {
        if (payment.OrderId is null || payment.WalletPaymentId is null || payment.PaidAt is null ||
            payment.PolicyId is null || payment.PolicyVersion is null || payment.PolicyHash is null)
            throw new InvalidOperationException("Captured payment lacks immutable settlement contract data.");
        var sourceKey = CaptureSource(payment.Id);
        var contract = new CreditPaymentCapturedV1(StableId(sourceKey),
            CreditPaymentSettlementEventTypes.CapturedV1, CreditPaymentSettlementEventTypes.Version1,
            payment.PaidAt.Value, CreditPaymentSettlementEventTypes.Source, payment.Id,
            payment.MerchantId, payment.TerminalId, supplierId, payment.MerchantOrderId,
            payment.OrderId.Value, payment.AmountMinor, payment.CapturedAmountMinor, payment.Currency,
            payment.WalletPaymentId.Value, payment.ApprovedCategoryCodeSnapshot, payment.PolicyId,
            payment.PolicyVersion, payment.PolicyHash);
        return CreditPaymentIntegrationEvent.Create(contract.EventId, sourceKey, contract.EventType,
            contract.EventVersion, contract.Source, "GatewayPayment", payment.Id, contract.OccurredAt,
            JsonSerializer.Serialize(contract, Json), createdAt);
    }

    public static CreditPaymentIntegrationEvent Refunded(Refund refund, GatewayPayment payment,
        Guid supplierId, DateTimeOffset createdAt)
    {
        if (payment.OrderId is null || payment.WalletPaymentId is null || refund.WalletRefundId is null ||
            refund.CompletedAt is null)
            throw new InvalidOperationException("Successful refund lacks immutable settlement contract data.");
        var sourceKey = RefundSource(refund.Id);
        var contract = new CreditPaymentRefundedV1(StableId(sourceKey),
            CreditPaymentSettlementEventTypes.RefundedV1, CreditPaymentSettlementEventTypes.Version1,
            refund.CompletedAt.Value, CreditPaymentSettlementEventTypes.Source, refund.Id, payment.Id,
            payment.MerchantId, payment.TerminalId, supplierId, payment.OrderId.Value,
            payment.WalletPaymentId.Value, refund.WalletRefundId.Value, refund.AmountMinor, refund.Currency,
            payment.CapturedAmountMinor, payment.RefundedAmountMinor,
            payment.Status == GatewayPaymentStatus.Refunded, refund.MerchantRefundReference);
        return CreditPaymentIntegrationEvent.Create(contract.EventId, sourceKey, contract.EventType,
            contract.EventVersion, contract.Source, "Refund", refund.Id, contract.OccurredAt,
            JsonSerializer.Serialize(contract, Json), createdAt);
    }

    private static Guid StableId(string sourceKey)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(sourceKey))[..16];
        bytes[6] = (byte)((bytes[6] & 0x0f) | 0x50);
        bytes[8] = (byte)((bytes[8] & 0x3f) | 0x80);
        return new Guid(bytes);
    }
}

public sealed class UnavailableCreditPaymentIntegrationEventPublisher
    : ICreditPaymentIntegrationEventPublisher
{
    public Task PublishAsync(CreditPaymentIntegrationEventMessage message, CancellationToken cancellationToken) =>
        throw new InvalidOperationException("No internal integration-event transport is configured.");
}

public sealed class CreditPaymentIntegrationEventWorkService(
    CreditPaymentsDbContext db,
    ICreditPaymentIntegrationEventPublisher publisher,
    IOptions<SettlementIntegrationOptions> configured,
    TimeProvider clock)
{
    public async Task<IReadOnlyList<Guid>> LeaseBatchAsync(string owner, CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        var options = configured.Value;
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var rows = await db.IntegrationEvents.FromSqlInterpolated($@"SELECT *
            FROM credit_payments.integration_events
            WHERE status <> 3 AND (next_retry_at IS NULL OR next_retry_at <= {now})
              AND (lease_until IS NULL OR lease_until <= {now})
            ORDER BY occurred_at,integration_event_id
            FOR UPDATE SKIP LOCKED LIMIT {Math.Clamp(options.BatchSize, 1, 500)}").ToListAsync(ct);
        foreach (var row in rows)
            row.AcquireLease(owner, now.AddSeconds(options.LeaseSeconds), now);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return rows.Select(x => x.Id).ToArray();
    }

    public async Task PublishOneAsync(Guid eventId, string owner, CancellationToken ct)
    {
        db.ChangeTracker.Clear();
        var row = await db.IntegrationEvents.AsNoTracking().SingleAsync(x => x.Id == eventId, ct);
        var message = new CreditPaymentIntegrationEventMessage(row.Id, row.EventType, row.EventVersion,
            row.OccurredAt, row.Source, row.PayloadJson);
        try
        {
            await publisher.PublishAsync(message, ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch
        {
            var failedAt = clock.GetUtcNow();
            var retry = await db.IntegrationEvents.SingleAsync(x => x.Id == eventId, ct);
            var exponent = Math.Min(retry.RetryCount, 20);
            var seconds = Math.Min(configured.Value.MaximumRetrySeconds,
                configured.Value.BaseRetrySeconds * Math.Pow(2, exponent));
            var jitter = 0.8 + Random.Shared.NextDouble() * 0.4;
            retry.ScheduleRetry(owner, "transport_publish_failed",
                failedAt.AddSeconds(seconds * jitter), failedAt);
            db.AuditRecords.Add(AuditRecord.Create("settlement-integration.publish-retry",
                "IntegrationEvent", retry.Id, null, failedAt));
            await db.SaveChangesAsync(ct);
            return;
        }

        // Transport acceptance precedes this local save. A failure here intentionally leaves the
        // same immutable EventId leased for at-least-once publication after lease expiry.
        var publishedAt = clock.GetUtcNow();
        var published = await db.IntegrationEvents.SingleAsync(x => x.Id == eventId, ct);
        published.MarkPublished(owner, publishedAt);
        db.AuditRecords.Add(AuditRecord.Create("settlement-integration.publish-succeeded",
            "IntegrationEvent", published.Id, null, publishedAt));
        await db.SaveChangesAsync(ct);
    }
}

public sealed class SettlementIntegrationEventReconciler(CreditPaymentsDbContext db, TimeProvider clock)
    : ICreditPaymentIntegrationEventReconciler
{
    public Task<int> ReconcileAsync(int batchSize, CancellationToken cancellationToken) =>
        ExecuteOnceAsync(batchSize, cancellationToken);
    public async Task<int> ExecuteOnceAsync(int batchSize, CancellationToken ct)
    {
        var added = await ReconcilePayments(Math.Clamp(batchSize, 1, 500), ct);
        added += await ReconcileRefunds(Math.Clamp(batchSize, 1, 500), ct);
        return added;
    }

    private async Task<int> ReconcilePayments(int size, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var rows = await db.GatewayPayments.FromSqlInterpolated($@"SELECT gp.*
            FROM credit_payments.gateway_payments gp
            WHERE gp.status IN (5,9,10)
              AND NOT EXISTS (SELECT 1 FROM credit_payments.integration_events ie
                  WHERE ie.source_key = 'credit-payment-captured:' || replace(gp.gateway_payment_id::text,'-',''))
            ORDER BY gp.paid_at,gp.gateway_payment_id
            FOR UPDATE SKIP LOCKED LIMIT {size}").ToListAsync(ct);
        var added = 0; var now = clock.GetUtcNow();
        foreach (var payment in rows)
        {
            var merchant = await db.Merchants.SingleAsync(x => x.Id == payment.MerchantId, ct);
            try
            {
                db.IntegrationEvents.Add(SettlementIntegrationEventFactory.Captured(payment, merchant.SupplierId, now));
                db.AuditRecords.Add(AuditRecord.Create("settlement-integration.historical-fact-reconciled",
                    "GatewayPayment", payment.Id, null, now));
                added++;
            }
            catch (InvalidOperationException)
            {
                await RecordIntegrityFailure("GatewayPayment", payment.Id, now, ct);
            }
        }
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return added;
    }

    private async Task<int> ReconcileRefunds(int size, CancellationToken ct)
    {
        db.ChangeTracker.Clear();
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var rows = await db.Refunds.FromSqlInterpolated($@"SELECT r.*
            FROM credit_payments.refunds r
            WHERE r.status = 4
              AND NOT EXISTS (SELECT 1 FROM credit_payments.integration_events ie
                  WHERE ie.source_key = 'credit-payment-refunded:' || replace(r.refund_id::text,'-',''))
            ORDER BY r.completed_at,r.refund_id
            FOR UPDATE SKIP LOCKED LIMIT {size}").ToListAsync(ct);
        var added = 0; var now = clock.GetUtcNow();
        foreach (var refund in rows)
        {
            var payment = await db.GatewayPayments.SingleAsync(x => x.Id == refund.GatewayPaymentId, ct);
            var merchant = await db.Merchants.SingleAsync(x => x.Id == payment.MerchantId, ct);
            try
            {
                db.IntegrationEvents.Add(SettlementIntegrationEventFactory.Refunded(refund, payment, merchant.SupplierId, now));
                db.AuditRecords.Add(AuditRecord.Create("settlement-integration.historical-fact-reconciled",
                    "Refund", refund.Id, null, now));
                added++;
            }
            catch (InvalidOperationException)
            {
                await RecordIntegrityFailure("Refund", refund.Id, now, ct);
            }
        }
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return added;
    }

    private async Task RecordIntegrityFailure(string type, Guid id, DateTimeOffset now, CancellationToken ct)
    {
        if (!await db.AuditRecords.AnyAsync(x => x.Action == "settlement-integration.contract-data-integrity-failure"
            && x.EntityType == type && x.EntityId == id, ct))
            db.AuditRecords.Add(AuditRecord.Create("settlement-integration.contract-data-integrity-failure",
                type, id, null, now));
    }
}

public sealed class CreditPaymentIntegrationEventPublisherWorker(
    IServiceScopeFactory scopes, IOptions<SettlementIntegrationOptions> configured,
    ILogger<CreditPaymentIntegrationEventPublisherWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!configured.Value.PublicationEnabled) return;
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(configured.Value.PublicationPollSeconds));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                using var scope = scopes.CreateScope();
                var work = scope.ServiceProvider.GetRequiredService<CreditPaymentIntegrationEventWorkService>();
                var owner = $"{Environment.MachineName}:{Guid.NewGuid():N}";
                foreach (var id in await work.LeaseBatchAsync(owner, stoppingToken))
                    await work.PublishOneAsync(id, owner, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
            catch (Exception ex) { logger.LogError(ex, "Credit payment integration-event publication failed."); }
        }
    }
}

public sealed class SettlementIntegrationEventReconciliationWorker(
    IServiceScopeFactory scopes, IOptions<SettlementIntegrationOptions> configured,
    ILogger<SettlementIntegrationEventReconciliationWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!configured.Value.HistoricalReconciliationEnabled) return;
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(configured.Value.ReconciliationPollSeconds));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                using var scope = scopes.CreateScope();
                var reconciler = scope.ServiceProvider.GetRequiredService<SettlementIntegrationEventReconciler>();
                await reconciler.ExecuteOnceAsync(configured.Value.BatchSize, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
            catch (Exception ex) { logger.LogError(ex, "Settlement integration-event reconciliation failed."); }
        }
    }
}
