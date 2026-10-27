using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Refahi.Modules.Entertainments.Application.Services;
using Refahi.Modules.Entertainments.Domain;
using Refahi.Modules.Entertainments.Infrastructure.Persistence;
using Refahi.Modules.Entertainments.Infrastructure.Providers.TouristPanel;
using Xunit;
using static Refahi.Modules.Entertainments.Tests.TouristPanelClientTests;

namespace Refahi.Modules.Entertainments.Tests;
public sealed class TouristPanelCatalogAndPricingTests
{
    [Theory]
    [InlineData(105, 10, 5, 121)]
    [InlineData(100, 0, 0, 100)]
    [InlineData(50, 1, 0, 51)]
    [InlineData(49, 1, 0, 49)]
    public void Retail_price_rounds_per_ticket_away_from_zero(long cost, int percent, long fixedMinor, long expected)
    {
        var result = new EntertainmentPricingService().Calculate(cost, percent, fixedMinor, "v2");
        Assert.Equal(expected, result.SaleMinor); Assert.Equal(expected - cost, result.MarkupMinor); Assert.Equal("v2", result.Version);
    }
    [Fact]
    public void Invalid_rial_and_overflow_are_rejected()
    {
        var p = new EntertainmentPricingService();
        Assert.Throws<EntertainmentDomainException>(() => p.Calculate(1.5m, 0, 0, "v1"));
        Assert.Throws<EntertainmentDomainException>(() => p.Calculate(100, -1, 0, "v1"));
        Assert.Throws<EntertainmentDomainException>(() => p.Calculate(100, 0, 0, ""));
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
}
