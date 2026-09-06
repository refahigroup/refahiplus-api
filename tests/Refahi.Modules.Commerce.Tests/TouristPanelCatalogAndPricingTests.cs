using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Refahi.Modules.Commerce.Application.Services;
using Refahi.Modules.Commerce.Domain;
using Refahi.Modules.Commerce.Infrastructure.Persistence;
using Refahi.Modules.Commerce.Infrastructure.Providers.TouristPanel;
using Xunit;
using static Refahi.Modules.Commerce.Tests.TouristPanelClientTests;

namespace Refahi.Modules.Commerce.Tests;
public sealed class TouristPanelCatalogAndPricingTests
{
    [Theory]
    [InlineData(105, 10, 5, 121)]
    [InlineData(100, 0, 0, 100)]
    [InlineData(50, 1, 0, 51)]
    [InlineData(49, 1, 0, 49)]
    public void Retail_price_rounds_per_ticket_away_from_zero(long cost, int percent, long fixedMinor, long expected)
    {
        var result = new CommercePricingService().Calculate(cost, percent, fixedMinor, "v2");
        Assert.Equal(expected, result.SaleMinor); Assert.Equal(expected - cost, result.MarkupMinor); Assert.Equal("v2", result.Version);
    }
    [Fact]
    public void Invalid_rial_and_overflow_are_rejected()
    {
        var p = new CommercePricingService();
        Assert.Throws<CommerceDomainException>(() => p.Calculate(1.5m, 0, 0, "v1"));
        Assert.Throws<CommerceDomainException>(() => p.Calculate(100, -1, 0, "v1"));
        Assert.Throws<CommerceDomainException>(() => p.Calculate(100, 0, 0, ""));
        Assert.Throws<OverflowException>(() => p.Calculate(long.MaxValue, 1, 0, "v1"));
        Assert.Throws<OverflowException>(() => p.Calculate(decimal.MaxValue, 0, 0, "v1"));
    }
    [Fact]
    public void Missing_financial_configuration_never_means_zero_markup()
    {
        Assert.False(new TouristPanelOptions().IsSaleConfigured);
        Assert.False(new TouristPanelOptions { SalesEnabled = true, SettlementConfirmed = true, BuyPriceConfirmed = true,
            PaymentMethod = 100, BankGateway = 0, AllowedPriceCategoryIds = [Guid.NewGuid().ToString()], DeliveryHosts = ["tickets.test"], PricingVersion = "v1" }.IsSaleConfigured);
    }
    [Theory] [InlineData(false)] [InlineData(true)]
    public async Task Catalog_reads_past_short_pages_and_publishes_only_after_empty_page(bool duplicate)
    {
        await using var db = new CommerceDbContext(new DbContextOptionsBuilder<CommerceDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var options = new TouristPanelOptions { CatalogPageSize = 20, CatalogMaxPages = 5 };
        var id = Guid.NewGuid().ToString(); var second = duplicate ? id : Guid.NewGuid().ToString(); var seller = Guid.NewGuid().ToString();
        var transport = new Transport(r =>
        {
            if (r.Path.EndsWith("connect/token")) return Json("{\"access_token\":\"token\",\"expires_in\":3600,\"token_type\":\"Bearer\"}");
            var page = System.Web.HttpUtility.ParseQueryString(r.Query)["page"];
            return Json(page == "3" ? "[]" : JsonSerializer.Serialize(new[] { new { id = page == "1" ? id : second, supplyChainHojreId = seller } }));
        });
        var catalog = new TouristPanelCatalog(db, Client(transport, options), Options.Create(options));
        if (duplicate)
        {
            await Assert.ThrowsAsync<InvalidOperationException>(() => catalog.RefreshAsync(default)); Assert.Empty(db.CatalogSnapshots);
        }
        else
        {
            await catalog.RefreshAsync(default); Assert.Equal(2, (await catalog.ReadAsync(default)).Count);
            Assert.Equal(3, transport.Requests.Count(x => x.Path.EndsWith("/programs")));
            await catalog.RefreshAsync(default); Assert.Equal(3, transport.Requests.Count(x => x.Path.EndsWith("/programs")));
        }
    }
    [Fact]
    public async Task Failed_refresh_preserves_last_successful_snapshot()
    {
        await using var db = new CommerceDbContext(new DbContextOptionsBuilder<CommerceDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var options = new TouristPanelOptions(); var snapshot = CommerceCatalogSnapshot.Create(options.AccountKey, "[]");
        db.CatalogSnapshots.Add(snapshot); await db.SaveChangesAsync();
        db.Entry(snapshot).Property(x => x.PublishedAt).CurrentValue = DateTimeOffset.UtcNow.AddHours(-1); await db.SaveChangesAsync();
        var published = snapshot.PublishedAt;
        var transport = new Transport(r => r.Path.EndsWith("connect/token") ? Json("{\"access_token\":\"token\",\"expires_in\":3600,\"token_type\":\"Bearer\"}") : Json("{}", HttpStatusCode.ServiceUnavailable));
        await Assert.ThrowsAsync<TouristPanelHttpException>(() => new TouristPanelCatalog(db, Client(transport, options), Options.Create(options)).RefreshAsync(default));
        Assert.Equal(published, (await db.CatalogSnapshots.SingleAsync()).PublishedAt); Assert.Equal("[]", snapshot.PayloadJson);
    }
}
