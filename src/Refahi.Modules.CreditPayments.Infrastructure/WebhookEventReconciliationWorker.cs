using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Refahi.Modules.CreditPayments.Application.Contracts;
using Refahi.Modules.CreditPayments.Domain;
using Refahi.Modules.CreditPayments.Infrastructure.Persistence;

namespace Refahi.Modules.CreditPayments.Infrastructure;

public sealed class WebhookEventReconciler(CreditPaymentsDbContext db, TimeProvider clock)
{
    public async Task<int> ExecuteOnceAsync(int batchSize, CancellationToken ct)
    {
        var count = await ReconcilePayments(Math.Clamp(batchSize, 1, 500), ct);
        count += await ReconcileRefunds(Math.Clamp(batchSize, 1, 500), ct);
        return count;
    }

    private async Task<int> ReconcilePayments(int size, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var rows = await db.GatewayPayments.FromSqlInterpolated($@"SELECT gp.*
            FROM credit_payments.gateway_payments gp
            WHERE gp.status IN (5,6,7,8,9,10)
            ORDER BY gp.updated_at,gp.gateway_payment_id
            FOR UPDATE SKIP LOCKED LIMIT {size}").ToListAsync(ct);
        var added = 0; var now = clock.GetUtcNow();
        foreach (var payment in rows)
        {
            var eventType = payment.Status switch
            {
                GatewayPaymentStatus.Succeeded or GatewayPaymentStatus.PartiallyRefunded or GatewayPaymentStatus.Refunded
                    => WebhookEventTypes.PaymentSucceeded,
                GatewayPaymentStatus.Failed => WebhookEventTypes.PaymentFailed,
                GatewayPaymentStatus.Canceled => WebhookEventTypes.PaymentCanceled,
                GatewayPaymentStatus.Expired => WebhookEventTypes.PaymentExpired,
                _ => null
            };
            if (eventType is null) continue;
            var source = $"{eventType}:{payment.Id:N}";
            if (await db.WebhookEvents.AnyAsync(x => x.SourceKey == source, ct)) continue;
            DateTimeOffset occurredAt;
            if (eventType == WebhookEventTypes.PaymentSucceeded) occurredAt = payment.PaidAt ?? payment.UpdatedAt;
            else if (eventType is WebhookEventTypes.PaymentCanceled or WebhookEventTypes.PaymentExpired)
                occurredAt = await db.PaymentTerminations.Where(x => x.GatewayPaymentId == payment.Id)
                    .Select(x => x.ResolvedAt).SingleOrDefaultAsync(ct) ?? payment.UpdatedAt;
            else occurredAt = payment.UpdatedAt;
            WebhookEventFactory.AddPayment(db, payment, eventType, occurredAt, now); added++;
        }
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return added;
    }

    private async Task<int> ReconcileRefunds(int size, CancellationToken ct)
    {
        db.ChangeTracker.Clear();
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var rows = await db.Refunds.FromSqlInterpolated($@"SELECT r.*
            FROM credit_payments.refunds r WHERE r.status IN (4,5)
            ORDER BY r.completed_at,r.refund_id FOR UPDATE SKIP LOCKED LIMIT {size}").ToListAsync(ct);
        var added = 0; var now = clock.GetUtcNow();
        foreach (var refund in rows)
        {
            var eventType = refund.Status == RefundStatus.Succeeded
                ? WebhookEventTypes.RefundSucceeded : WebhookEventTypes.RefundFailed;
            var source = $"{eventType}:{refund.Id:N}";
            if (await db.WebhookEvents.AnyAsync(x => x.SourceKey == source, ct)) continue;
            var payment = await db.GatewayPayments.SingleAsync(x => x.Id == refund.GatewayPaymentId, ct);
            WebhookEventFactory.AddRefund(db, refund, payment, eventType,
                refund.CompletedAt ?? refund.CreatedAt, now); added++;
        }
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return added;
    }
}

public sealed class WebhookEventReconciliationWorker(IServiceScopeFactory scopes,
    IOptions<WebhookOptions> configured, ILogger<WebhookEventReconciliationWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(
            Math.Max(5, configured.Value.ReconciliationPollSeconds)));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                using var scope = scopes.CreateScope();
                var reconciler = scope.ServiceProvider.GetRequiredService<WebhookEventReconciler>();
                await reconciler.ExecuteOnceAsync(Math.Clamp(configured.Value.BatchSize, 1, 500), stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
            catch (Exception ex) { logger.LogError(ex, "Webhook event reconciliation failed."); }
        }
    }
}
