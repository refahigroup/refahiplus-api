using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Refahi.Modules.Orders.Application.Contracts.IntegrationEvents;
using Refahi.Modules.Orders.Domain.Aggregates;
using Refahi.Modules.Orders.Domain.Entities;
using Refahi.Modules.Orders.Domain.Events;
using Refahi.Modules.Orders.Infrastructure.Outbox;
using Refahi.Modules.Orders.Infrastructure.Persistence.Configurations;

namespace Refahi.Modules.Orders.Infrastructure.Persistence.Context;

public class OrdersDbContext : DbContext
{
    public OrdersDbContext(DbContextOptions<OrdersDbContext> options)
        : base(options) { }

    public DbSet<OrderPromotionOperation> PromotionOperations => Set<OrderPromotionOperation>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderItem> OrderItems => Set<OrderItem>();
    public DbSet<OrderPaymentPosting> OrderPaymentPostings => Set<OrderPaymentPosting>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.HasDefaultSchema("orders");
        modelBuilder.Entity<OrderPromotionOperation>(e => { e.ToTable("promotion_operations"); e.HasKey(x => x.Id); e.Property(x => x.IdempotencyKey).HasMaxLength(200); e.HasIndex(x => new { x.OrderId, x.IdempotencyKey }).IsUnique(); e.HasIndex(x => x.CreatedAt).HasFilter("\"ResultJson\" IS NULL"); });

        modelBuilder.ApplyConfiguration(new OrderConfiguration());
        modelBuilder.ApplyConfiguration(new OrderItemConfiguration());
        modelBuilder.ApplyConfiguration(new OrderPaymentPostingConfiguration());
        modelBuilder.ApplyConfiguration(new OutboxMessageConfiguration());
    }

    /// <summary>
    /// Domain events را به outbox_messages منتقل می‌کند — همان transaction، تضمین at-least-once delivery
    /// </summary>
    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        var domainEventEntities = ChangeTracker
            .Entries<Order>()
            .Where(e => e.Entity.DomainEvents.Count > 0)
            .Select(e => e.Entity)
            .ToList();

        var outboxMessages = domainEventEntities
            .SelectMany(e => e.DomainEvents.Select(evt => MapToOutboxMessage(evt, e)))
            .Where(message => message is not null)
            .Select(message => message!)
            .ToList();

        static OutboxMessage? MapToOutboxMessage(Refahi.Shared.Domain.IDomainEvent evt, Order order)
        {
            var integrationEvent = MapToIntegrationEvent(evt, order);
            if (integrationEvent is null)
                return null;

            return new OutboxMessage
            {
                Id = Guid.NewGuid(),
                EventType = integrationEvent.GetType().AssemblyQualifiedName!,
                EventData = JsonSerializer.Serialize(integrationEvent, integrationEvent.GetType()),
                OccurredAt = evt.OccurredAt,
                RetryCount = 0,
                Status = OutboxMessageStatus.Pending,
            };
        }

        static object? MapToIntegrationEvent(Refahi.Shared.Domain.IDomainEvent evt, Order order) =>
            evt switch
            {
                OrderPaidEvent paid => new OrderPaidIntegrationEvent(
                    paid.OrderId,
                    paid.OrderNumber,
                    paid.UserId,
                    paid.SourceModule,
                    paid.SourceReferenceId,
                    paid.ReferenceType,
                    paid.SagaId,
                    paid.PaymentId,
                    paid.AmountMinor,
                    paid.OccurredAt,
                    paid.SourceAmountMinor,
                    paid.AmountBeforePromotionMinor,
                    paid.PromotionFundingMinor
                ),
                OrderCancelledEvent cancelled => new OrderCancelledIntegrationEvent(cancelled.OrderId, cancelled.OrderNumber, cancelled.UserId, order.SourceModule, order.SourceReferenceId, order.ReferenceType, cancelled.PaymentAction, cancelled.OccurredAt),
                OrderRefundedEvent refunded => new OrderRefundedIntegrationEvent(refunded.OrderId, refunded.OrderNumber, refunded.SourceOwnerId, refunded.SourceShopId, refunded.AmountMinor, refunded.OccurredAt),
                _ => evt,
            };

        if (outboxMessages.Count > 0)
            OutboxMessages.AddRange(outboxMessages);

        // پاک کردن domain events قبل از save — از اجرای مجدد جلوگیری می‌کند
        domainEventEntities.ForEach(e => e.ClearDomainEvents());

        return await base.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// برای ProcessOutboxMessagesJob: SaveChanges بدون interceptor domain events
    /// جلوگیری از loop بی‌نهایت در background job
    /// </summary>
    internal Task<int> SaveOutboxChangesAsync(CancellationToken cancellationToken = default) =>
        base.SaveChangesAsync(cancellationToken);
}
