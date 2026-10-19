using Microsoft.EntityFrameworkCore;
using Refahi.Modules.Cinema.Domain;
using Refahi.Modules.Cinema.Infrastructure.Persistence;
using Xunit;

namespace Refahi.Modules.Cinema.Tests;

public sealed class CinemaPersistenceTests
{
    [Theory]
    [InlineData("confirm")]
    [InlineData("cancel")]
    public void New_attempt_on_existing_order_is_tracked_as_insert(string operation)
    {
        var options = new DbContextOptionsBuilder<CinemaDbContext>()
            .UseNpgsql("Host=localhost;Database=unused;Username=unused").Options;
        using var db = new CinemaDbContext(options);
        var order = ReservedOrder();
        db.Attach(order);

        var attempt = order.BeginAttempt(operation);
        db.ChangeTracker.DetectChanges();

        Assert.Equal(EntityState.Added, db.Entry(attempt).State);
        Assert.Equal(EntityState.Unchanged, db.Entry(order.Attempts.Single(x => x.Operation == "reserve")).State);
    }

    [CinemaPostgresFact]
    public async Task New_attempts_on_existing_orders_are_inserted_and_can_be_completed_on_postgres()
    {
        var options = new DbContextOptionsBuilder<CinemaDbContext>()
            .UseNpgsql(Environment.GetEnvironmentVariable("CINEMA_TEST_POSTGRES")!).Options;
        await using var db = new CinemaDbContext(options);
        await db.Database.MigrateAsync();
        var repository = new CinemaOrderRepository(db);

        foreach (var operation in new[] { "confirm", "cancel" })
        {
            var order = ReservedOrder();
            await repository.AddAsync(order, default);
            db.ChangeTracker.Clear();
            order = (await repository.GetAsync(order.Id, default))!;
            order.MarkPaid();
            await repository.SaveAsync(default);

            var attempt = order.BeginAttempt(operation);
            await repository.SaveAsync(default);
            db.ChangeTracker.Clear();
            order = (await repository.GetAsync(order.Id, default))!;
            var savedAttempt = Assert.Single(order.Attempts, x => x.Operation == operation);
            Assert.Equal(attempt.Id, savedAttempt.Id);
            Assert.Equal("Started", savedAttempt.Outcome);
            savedAttempt.Complete();
            await repository.SaveAsync(default);

            db.ChangeTracker.Clear();
            order = (await repository.GetAsync(order.Id, default))!;
            Assert.Equal("Paid", order.PaymentStatus);
            Assert.Equal(2, order.Attempts.Count);
            Assert.All(order.Attempts, x => Assert.Equal("Completed", x.Outcome));
        }
    }

    private static CinemaOrder ReservedOrder()
    {
        var order = CinemaOrder.Create(Guid.NewGuid(), "iticket", "schedule", Guid.NewGuid().ToString("N"), "hash", "{}", "[]", "cinema");
        order.BeginAttempt("reserve").Complete();
        order.Reserve("provider-" + order.Id, "ticket", DateTimeOffset.UtcNow.AddMinutes(10), 100, 0, 0, 0, 100);
        return order;
    }
}
