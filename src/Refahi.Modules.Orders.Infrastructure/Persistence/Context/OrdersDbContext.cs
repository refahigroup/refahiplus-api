using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Refahi.Modules.Orders.Domain.Aggregates;
using Refahi.Modules.Orders.Domain.Entities;
using Refahi.Modules.Orders.Infrastructure.Outbox;
using Refahi.Modules.Orders.Infrastructure.Persistence.Configurations;

namespace Refahi.Modules.Orders.Infrastructure.Persistence.Context;

public class OrdersDbContext : DbContext
{
    public OrdersDbContext(DbContextOptions<OrdersDbContext> options) : base(options) { }

    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderItem> OrderItems => Set<OrderItem>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.HasDefaultSchema("orders");

        modelBuilder.ApplyConfiguration(new OrderConfiguration());
        modelBuilder.ApplyConfiguration(new OrderItemConfiguration());
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
            .SelectMany(e => e.DomainEvents)
            .Select(evt => new OutboxMessage
            {
                Id = Guid.NewGuid(),
                EventType = evt.GetType().AssemblyQualifiedName!,
                EventData = JsonSerializer.Serialize(evt, evt.GetType()),
                OccurredAt = evt.OccurredAt
            })
            .ToList();

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
    internal Task<int> SaveOutboxChangesAsync(CancellationToken cancellationToken = default)
        => base.SaveChangesAsync(cancellationToken);
}
