using Microsoft.Extensions.Configuration;
using Refahi.Modules.Commerce.Infrastructure.Providers.TouristPanel;
using Xunit;

namespace Refahi.Modules.Commerce.Tests;

public sealed class TouristPanelOptionsTests
{
    [Fact]
    public void Disabled_provider_has_no_implicit_sale_configuration()
    {
        var options = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Commerce:Providers:TouristPanel:Enabled"] = "false"
        }).Build().GetSection(TouristPanelOptions.SectionName).Get<TouristPanelOptions>()!;
        options.Validate();
        Assert.False(options.Enabled); Assert.False(options.SalesEnabled); Assert.False(options.IsSaleConfigured);
    }

    [Fact]
    public void Explicit_zero_markup_is_valid_when_every_contract_gate_is_configured()
    {
        var options = new TouristPanelOptions
        {
            Enabled = true, SalesEnabled = true, Tenant = Guid.NewGuid().ToString(), ClientId = "client", ClientSecret = "secret",
            Username = "user", Password = "password", SettlementConfirmed = true, BuyPriceConfirmed = true,
            PaymentMethod = 100, BankGateway = 0, AllowedPriceCategoryIds = [Guid.NewGuid().ToString()],
            CategoryCodes = new() { [1] = "entertainment" }, MarkupPercent = 0, MarkupFixedMinor = 0,
            PricingVersion = "zero-v1", DeliveryHosts = ["tickets.example.test"]
        };
        options.Validate(); Assert.True(options.IsSaleConfigured);
    }

    [Theory]
    [InlineData(999, 0)]
    [InlineData(100, 999)]
    public void Unknown_payment_enum_blocks_startup(int payment, int gateway)
    {
        var options = new TouristPanelOptions
        {
            Enabled = true, SalesEnabled = true, Tenant = Guid.NewGuid().ToString(), ClientId = "client", ClientSecret = "secret",
            Username = "user", Password = "password", SettlementConfirmed = true, BuyPriceConfirmed = true,
            PaymentMethod = payment, BankGateway = gateway, AllowedPriceCategoryIds = [Guid.NewGuid().ToString()],
            CategoryCodes = new() { [1] = "entertainment" }, MarkupPercent = 0, MarkupFixedMinor = 0,
            PricingVersion = "v1", DeliveryHosts = ["tickets.example.test"]
        };
        Assert.Throws<InvalidOperationException>(options.Validate);
    }
}
