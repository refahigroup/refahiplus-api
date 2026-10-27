using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Refahi.Modules.Commerce.Application.Services;
using Refahi.Modules.Commerce.Domain;
using Refahi.Modules.Commerce.Infrastructure.Persistence;
using Xunit;

namespace Refahi.Modules.Commerce.Tests;
public sealed class CommercePricingTests
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
}
