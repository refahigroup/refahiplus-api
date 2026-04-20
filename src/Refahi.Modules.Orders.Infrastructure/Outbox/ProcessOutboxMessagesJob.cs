using System.Text.Json;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Refahi.Modules.Orders.Infrastructure.Persistence.Context;

namespace Refahi.Modules.Orders.Infrastructure.Outbox;

public class ProcessOutboxMessagesJob : BackgroundService
{
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
            .Where(m => m.ProcessedAt == null)
            .OrderBy(m => m.OccurredAt)
            .Take(20)
            .ToListAsync(ct);

        if (messages.Count == 0) return;

        foreach (var message in messages)
        {
            try
            {
                var eventType = Type.GetType(message.EventType);
                if (eventType is null)
                {
                    _logger.LogWarning("نوع رویداد outbox یافت نشد: {Type}", message.EventType);
                    message.Error = $"Type not found: {message.EventType}";
                    message.ProcessedAt = DateTimeOffset.UtcNow;
                    continue;
                }

                var evt = JsonSerializer.Deserialize(message.EventData, eventType);
                if (evt is INotification notification)
                    await publisher.Publish(notification, ct);

                message.ProcessedAt = DateTimeOffset.UtcNow;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "خطا در پردازش outbox message {Id}", message.Id);
                message.Error = ex.Message[..Math.Min(ex.Message.Length, 2000)];
                message.ProcessedAt = DateTimeOffset.UtcNow;
            }
        }

        // از SaveOutboxChangesAsync استفاده می‌کنیم تا domain event interceptor مجدداً اجرا نشود
        await context.SaveOutboxChangesAsync(ct);
    }
}
