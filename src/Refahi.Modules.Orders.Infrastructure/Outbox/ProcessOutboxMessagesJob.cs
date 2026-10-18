using System.Text.Json;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Refahi.Modules.Orders.Infrastructure.Persistence.Context;
namespace Refahi.Modules.Orders.Infrastructure.Outbox;

public sealed class ProcessOutboxMessagesJob(IServiceScopeFactory scopes, ILogger<ProcessOutboxMessagesJob> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(10));
        while (await timer.WaitForNextTickAsync(ct))
        {
            try { for (var i = 0; i < 20; i++) if (!await ProcessOne(ct)) break; }
            catch (Exception ex) when (!ct.IsCancellationRequested) { logger.LogError(ex, "Outbox scan failed; retrying next interval"); }
        }
    }
    private async Task<bool> ProcessOne(CancellationToken ct)
    {
        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OrdersDbContext>();
        OutboxMessage? message;
        await using (var tx = await db.Database.BeginTransactionAsync(ct))
        {
            message = await db.OutboxMessages.FromSqlRaw("""
                select * from orders.outbox_messages
                where processed_at is null and retry_count < 10 and
                    (status='Pending' or (status='Processing' and (lease_until is null or lease_until < now())))
                    and (next_attempt_at is null or next_attempt_at <= now())
                order by occurred_at for update skip locked limit 1
                """).FirstOrDefaultAsync(ct);
            if (message == null) return false;
            message.Status = OutboxMessageStatus.Processing; message.LeaseId = Guid.NewGuid();
            message.LeaseUntil = DateTimeOffset.UtcNow.AddMinutes(5);
            await db.SaveOutboxChangesAsync(ct); await tx.CommitAsync(ct);
        }
        var lease = message.LeaseId;
        try
        {
            var type = Type.GetType(message.EventType) ?? throw new InvalidOperationException("نوع رویداد ثبت‌شده یافت نشد");
            var value = JsonSerializer.Deserialize(message.EventData, type);
            if (value is Refahi.Modules.Orders.Domain.Events.OrderPaidEvent legacy)
            {
                var order = await db.Orders.AsNoTracking().SingleOrDefaultAsync(x => x.Id == legacy.OrderId, ct);
                value = new Refahi.Modules.Orders.Application.Contracts.IntegrationEvents.OrderPaidIntegrationEvent(
                    legacy.OrderId, legacy.OrderNumber, legacy.UserId, order?.SourceModule ?? legacy.SourceModule,
                    order?.SourceReferenceId ?? legacy.SourceReferenceId, order?.ReferenceType ?? legacy.ReferenceType,
                    order?.SagaId ?? legacy.SagaId, legacy.PaymentId, legacy.AmountMinor, legacy.OccurredAt,
                    order?.SourceAmountMinor ?? legacy.SourceAmountMinor, order?.AmountBeforePromotionMinor ?? legacy.AmountBeforePromotionMinor,
                    order?.DiscountCodeAmountMinor ?? legacy.PromotionFundingMinor);
            }
            if (value is Refahi.Modules.Orders.Application.Contracts.IntegrationEvents.OrderPaidIntegrationEvent paid)
            {
                var current = await db.Orders.AsNoTracking().SingleOrDefaultAsync(x => x.Id == paid.OrderId, ct);
                if (current == null || current.PaymentState != Refahi.Modules.Orders.Domain.Enums.PaymentState.Paid || current.PaymentId != paid.PaymentId)
                    value = null;
            }
            if (value is INotification notification) await scope.ServiceProvider.GetRequiredService<IPublisher>().Publish(notification, ct);
            await db.OutboxMessages.Where(x => x.Id == message.Id && x.LeaseId == lease).ExecuteUpdateAsync(s => s
                .SetProperty(x => x.Status, OutboxMessageStatus.Processed).SetProperty(x => x.ProcessedAt, DateTimeOffset.UtcNow)
                .SetProperty(x => x.LeaseId, (Guid?)null).SetProperty(x => x.LeaseUntil, (DateTimeOffset?)null).SetProperty(x => x.Error, (string?)null), ct);
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            logger.LogError(ex, "Outbox delivery failed. OutboxMessageId={OutboxMessageId} RetryCount={RetryCount}", message.Id, message.RetryCount);
            var retry = message.RetryCount + 1;
            var status = retry >= 10 ? OutboxMessageStatus.DeadLettered : OutboxMessageStatus.Pending;
            var error = ex.GetType().Name;
            var next = DateTimeOffset.UtcNow.AddSeconds(Math.Min(300, Math.Pow(2, retry)));
            await db.OutboxMessages.Where(x => x.Id == message.Id && x.LeaseId == lease).ExecuteUpdateAsync(s => s
                .SetProperty(x => x.Status, status).SetProperty(x => x.RetryCount, retry).SetProperty(x => x.Error, error)
                .SetProperty(x => x.NextAttemptAt, next).SetProperty(x => x.LeaseId, (Guid?)null).SetProperty(x => x.LeaseUntil, (DateTimeOffset?)null), ct);
        }
        return true;
    }
}
