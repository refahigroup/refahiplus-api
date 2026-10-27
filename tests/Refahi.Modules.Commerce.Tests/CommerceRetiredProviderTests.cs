using Microsoft.EntityFrameworkCore;
using Refahi.Modules.Commerce.Domain;
using Refahi.Modules.Commerce.Infrastructure.Persistence;
using Xunit;
namespace Refahi.Modules.Commerce.Tests;

public sealed class CommerceRetiredProviderTests
{
    [Fact]
    public async Task Old_touristpanel_test_orders_cannot_starve_the_aabsar_fulfillment_queue()
    {
        await using var db = new CommerceDbContext(new DbContextOptionsBuilder<CommerceDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        for (var i = 0; i < 12; i++)
        {
            var provider = i == 11 ? "aabsar" : "touristpanel";
            var order = CommerceOrder.Create(Guid.NewGuid(), "key-" + i, "hash", "n", "m",
                [new(provider, provider, "program", "offer", "option", "title", "offer", "entertainment", 1, 100, "{}")]);
            order.QueueFulfillment(Guid.NewGuid()); db.Orders.Add(order);
        }
        await db.SaveChangesAsync();
        var pending = await new CommerceRepository(db).GetPendingFulfillmentAsync(10);
        Assert.Equal("aabsar", Assert.Single(pending).Items.Single().ProviderKey);
    }
}
