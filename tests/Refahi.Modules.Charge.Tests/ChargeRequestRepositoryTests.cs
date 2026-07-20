using Microsoft.EntityFrameworkCore;
using Refahi.Modules.Charge.Domain.Aggregates;
using Refahi.Modules.Charge.Domain.Enums;
using Refahi.Modules.Charge.Infrastructure.Persistence.Context;
using Refahi.Modules.Charge.Infrastructure.Persistence.Repositories;

namespace Refahi.Modules.Charge.Tests;

public sealed class ChargeRequestRepositoryTests
{
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
