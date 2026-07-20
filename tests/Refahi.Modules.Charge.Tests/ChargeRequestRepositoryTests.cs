using Microsoft.EntityFrameworkCore;
using Refahi.Modules.Charge.Domain.Aggregates;
using Refahi.Modules.Charge.Domain.Enums;
using Refahi.Modules.Charge.Infrastructure.Persistence.Context;
using Refahi.Modules.Charge.Infrastructure.Persistence.Repositories;

namespace Refahi.Modules.Charge.Tests;

public sealed class ChargeRequestRepositoryTests
{
    [Fact]
    public async Task AddPinToTrackedRequest_TracksPinAsAdded()
    {
        var options = new DbContextOptionsBuilder<ChargeDbContext>()
            .UseNpgsql("Host=localhost;Database=refahi_charge_state_test;Username=test;Password=test")
            .Options;
        await using var context = new ChargeDbContext(options);
        var now = DateTime.UtcNow;
        var request = ChargeRequest.Create(
            Guid.NewGuid(),
            "Eniac",
            ChargeOperator.Irancell,
            ChargeServiceType.PinCharge,
            "09350000000",
            "09000000000",
            "IrancellCharge_50000",
            "شارژ ایرانسل",
            1003,
            0,
            1005,
            1,
            "{}",
            50_000,
            null,
            0,
            0,
            0,
            50_000,
            Guid.NewGuid().ToString("N"),
            now,
            now.AddMinutes(20));
        context.Attach(request);

        request.AddPin("encrypted-serial", "encrypted-code", 50_000);
        context.ChangeTracker.DetectChanges();

        var pin = Assert.Single(request.Pins);
        Assert.Equal(EntityState.Added, context.Entry(pin).State);
    }

    [Fact]
    public async Task AddFulfillmentAttemptAsync_TracksNewAttemptAsAdded()
    {
        var options = new DbContextOptionsBuilder<ChargeDbContext>()
            .UseNpgsql("Host=localhost;Database=refahi_charge_state_test;Username=test;Password=test")
            .Options;
        await using var context = new ChargeDbContext(options);
        var repository = new ChargeRequestRepository(context);
        var requestId = Guid.NewGuid();
        var attempt = ChargeFulfillmentAttempt.Create(
            requestId,
            FulfillmentAttemptType.Trace,
            false,
            117,
            null,
            null,
            null,
            "اطلاعاتی برای نمایش وجود ندارد",
            "{}",
            "{}",
            100,
            DateTime.UtcNow);

        await repository.AddFulfillmentAttemptAsync(attempt);

        Assert.Equal(EntityState.Added, context.Entry(attempt).State);
    }
}
