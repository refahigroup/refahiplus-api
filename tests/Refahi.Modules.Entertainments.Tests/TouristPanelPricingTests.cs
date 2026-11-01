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
    [Theory]
    [InlineData(100, 10, 5, 20, 3, 92, 15, 23)]
    [InlineData(50, 0, 0, 1, 0, 49, 0, 1)]
    [InlineData(49, 0, 0, 1, 0, 49, 0, 0)]
    [InlineData(100, 0, 0, 99, 0, 1, 0, 99)]
    public void Discounts_use_cost_basis_and_allow_positive_subsidized_prices(long cost, int percent, long fixedMinor,
        int discountPercent, long discountFixedMinor, long sale, long markup, long discount)
    {
        var result = new EntertainmentPricingService().Calculate(cost, percent, fixedMinor, "discount-v1", discountPercent, discountFixedMinor);
        Assert.Equal(sale, result.SaleMinor); Assert.Equal(markup, result.MarkupMinor); Assert.Equal(discount, result.DiscountMinor);
    }
    [Theory][InlineData(0,0,0)][InlineData(100,100,0)][InlineData(100,101,0)][InlineData(100,-1,0)][InlineData(100,0,-1)]
    public void Free_invalid_or_negative_discount_sales_are_rejected(long cost, int percent, long fixedMinor) =>
        Assert.Throws<EntertainmentDomainException>(() => new EntertainmentPricingService().Calculate(cost, 0, 0, "v1", percent, fixedMinor));

    [Fact]
    public void Historical_price_snapshots_default_discounts_to_zero()
    {
        var value = JsonSerializer.Deserialize<Refahi.Modules.Entertainments.Application.Contracts.Providers.EntertainmentPrice>(
            "{\"ProviderCostMinor\":100,\"MarkupMinor\":10,\"SaleMinor\":110,\"Version\":\"v1\"}")!;
        Assert.Equal(0, value.DiscountMinor);
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
            PaymentMethod = 100, BankGateway = 0, DeliveryHosts = ["tickets.test"], PricingVersion = "v1" }.IsSaleConfigured);
    }
}
