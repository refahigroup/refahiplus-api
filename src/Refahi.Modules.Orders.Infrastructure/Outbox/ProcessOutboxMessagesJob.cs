using System.Text.Json;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Refahi.Modules.Orders.Application.Contracts.IntegrationEvents;
using Refahi.Modules.Orders.Domain.Events;
using Refahi.Modules.Orders.Infrastructure.Persistence.Context;

namespace Refahi.Modules.Orders.Infrastructure.Outbox;

public class ProcessOutboxMessagesJob : BackgroundService
{
    private const int MaxRetryCount = 10;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ProcessOutboxMessagesJob> _logger;
    private static readonly TimeSpan _interval = TimeSpan.FromSeconds(10);

    public ProcessOutboxMessagesJob(IServiceScopeFactory scopeFactory, ILogger<ProcessOutboxMessagesJob> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(_interval);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await ProcessPendingMessagesAsync(stoppingToken);
        }
    }

    private async Task ProcessPendingMessagesAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<OrdersDbContext>();
        var publisher = scope.ServiceProvider.GetRequiredService<IPublisher>();

        var messages = await context.OutboxMessages
            .Where(m => m.ProcessedAt == null &&
                        m.Status == OutboxMessageStatus.Pending &&
                        m.RetryCount < MaxRetryCount)
            .OrderBy(m => m.OccurredAt)
            .Take(20)
            .ToListAsync(ct);

        if (messages.Count == 0) return;

        foreach (var message in messages)
        {
            try
            {
                message.Status = OutboxMessageStatus.Processing;
                message.Error = null;
                await context.SaveOutboxChangesAsync(ct);

                var eventType = Type.GetType(message.EventType);
                if (eventType is null)
                {
                    _logger.LogWarning(
                        "Outbox message type was not found. OutboxMessageId={OutboxMessageId}, EventType={EventType}, RetryCount={RetryCount}",
                        message.Id,
                        message.EventType,
                        message.RetryCount);
                    message.Error = $"Type not found: {message.EventType}";
                    message.RetryCount++;
                    message.Status = message.RetryCount >= MaxRetryCount
                        ? OutboxMessageStatus.DeadLettered
                        : OutboxMessageStatus.Pending;
                    continue;
                }

                var evt = JsonSerializer.Deserialize(message.EventData, eventType);
                if (evt is OrderPaidEvent paidEvent)
                {
                    var sourceModule = paidEvent.SourceModule;
                    var sourceReferenceId = paidEvent.SourceReferenceId;
                    var referenceType = paidEvent.ReferenceType;
                    var sagaId = paidEvent.SagaId;

                    if (string.IsNullOrWhiteSpace(sourceModule) || string.IsNullOrWhiteSpace(referenceType))
                    {
                        var order = await context.Orders
                            .AsNoTracking()
                            .FirstOrDefaultAsync(o => o.Id == paidEvent.OrderId, ct);

                        if (order is not null)
                        {
                            sourceModule = order.SourceModule;
                            sourceReferenceId = order.SourceReferenceId;
                            referenceType = order.ReferenceType;
                            sagaId = order.SagaId;
                        }
                    }

                    using var logScope = _logger.BeginScope(new Dictionary<string, object?>
                    {
                        ["SagaId"] = sagaId,
                        ["OrderId"] = paidEvent.OrderId,
                        ["UserId"] = paidEvent.UserId,
                        ["HotelRequestId"] = sourceReferenceId,
                        ["ProviderBookingCode"] = null
                    });

                    _logger.LogInformation(
                        "Publishing OrderPaidIntegrationEvent from outbox. OutboxMessageId={OutboxMessageId}, RetryCount={RetryCount}",
                        message.Id,
                        message.RetryCount);

                    await publisher.Publish(new OrderPaidIntegrationEvent(
                        paidEvent.OrderId,
                        paidEvent.OrderNumber,
                        paidEvent.UserId,
                        sourceModule ?? string.Empty,
                        sourceReferenceId,
                        referenceType ?? string.Empty,
                        sagaId,
                        paidEvent.PaymentId,
                        paidEvent.AmountMinor,
                        paidEvent.OccurredAt), ct);
                }
                else if (evt is INotification notification)
                {
                    await publisher.Publish(notification, ct);
                }

                message.Status = OutboxMessageStatus.Processed;
                message.ProcessedAt = DateTimeOffset.UtcNow;
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Outbox message processing failed. OutboxMessageId={OutboxMessageId}, EventType={EventType}, RetryCount={RetryCount}",
                    message.Id,
                    message.EventType,
                    message.RetryCount);
                message.Error = ex.Message[..Math.Min(ex.Message.Length, 2000)];
                message.RetryCount++;
                message.Status = message.RetryCount >= MaxRetryCount
                    ? OutboxMessageStatus.DeadLettered
                    : OutboxMessageStatus.Pending;
            }
        }

        // از SaveOutboxChangesAsync استفاده می‌کنیم تا domain event interceptor مجدداً اجرا نشود
        await context.SaveOutboxChangesAsync(ct);
    }
}
