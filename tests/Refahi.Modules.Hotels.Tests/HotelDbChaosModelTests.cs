using Microsoft.EntityFrameworkCore;
using Refahi.Modules.Hotels.Domain.Aggregates.HotelBookingSagaAgg;
using Refahi.Modules.Hotels.Domain.Aggregates.ProviderBookingCacheAgg;
using Refahi.Modules.Hotels.Infrastructure.Persistence;
using Xunit;

namespace Refahi.Modules.Hotels.Tests;

public sealed class HotelDbChaosModelTests
{
    [Fact]
    public void DbModel_HasCrashRecoveryUniquenessAndCancellationIndexes()
    {
        var options = new DbContextOptionsBuilder<HotelsDbContext>()
            .UseNpgsql("Host=localhost;Database=refahi_chaos_metadata;Username=refahi;Password=refahi")
            .Options;

        using var dbContext = new HotelsDbContext(options);
        var model = dbContext.Model;

        var saga = model.FindEntityType(typeof(HotelBookingSagaState))
            ?? throw new InvalidOperationException("HotelBookingSagaState model was not registered.");
        var cache = model.FindEntityType(typeof(HotelProviderBookingCacheEntry))
            ?? throw new InvalidOperationException("HotelProviderBookingCacheEntry model was not registered.");

        Assert.Contains(saga.GetIndexes(), index =>
            index.IsUnique &&
            index.Properties.Select(p => p.Name).SequenceEqual(["HotelRequestId"]));

        Assert.Contains(saga.GetIndexes(), index =>
            index.IsUnique &&
            index.Properties.Select(p => p.Name).SequenceEqual(["OrderId"]) &&
            index.GetFilter() == "\"order_id\" IS NOT NULL");

        Assert.Contains(saga.GetIndexes(), index =>
            index.Properties.Select(p => p.Name).SequenceEqual(["ProviderBookingStatus", "UpdatedAt"]));

        Assert.NotNull(saga.FindProperty("ProviderCancellationIdempotencyKey"));
        Assert.NotNull(saga.FindProperty("ProviderCancellationReason"));
        Assert.NotNull(saga.FindProperty("ProviderCancellationRequestedAt"));
        Assert.NotNull(saga.FindProperty("ProviderCancellationCompletedAt"));
        Assert.NotNull(saga.FindProperty("ExternalUnresolvedAt"));

        Assert.Contains(cache.GetIndexes(), index =>
            index.IsUnique &&
            index.Properties.Select(p => p.Name).SequenceEqual(["ProviderName", "IdempotencyKey"]));

        Assert.Contains(cache.GetIndexes(), index =>
            index.IsUnique &&
            index.Properties.Select(p => p.Name).SequenceEqual(["ProviderName", "ProviderBookingCode"]) &&
            index.GetFilter() == "\"provider_booking_code\" IS NOT NULL");

        Assert.Contains(cache.GetIndexes(), index =>
            index.Properties.Select(p => p.Name).SequenceEqual(["CancellationIdempotencyKey"]) &&
            index.GetFilter() == "\"cancellation_idempotency_key\" IS NOT NULL");

        Assert.NotNull(cache.FindProperty("CancellationIdempotencyKey"));
        Assert.NotNull(cache.FindProperty("CancellationReason"));
        Assert.NotNull(cache.FindProperty("CancellationRequestedAt"));
        Assert.NotNull(cache.FindProperty("CancellationCompletedAt"));
        Assert.NotNull(cache.FindProperty("ExternalUnresolvedAt"));
    }
}
