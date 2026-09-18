using Refahi.Modules.CreditPayments.Application.Contracts;

namespace Refahi.Modules.CreditPayments.Tests;

public sealed class Phase11SecurityTests
{
    [Fact]
    public void Admin_read_models_do_not_expose_secret_material()
    {
        var forbidden=new[]{"secret","hash","cipher","token","nonce"};
        var types=new[]{typeof(AdminPaymentSummaryDto),typeof(AdminPaymentDetailsDto),typeof(AdminRefundDto),
            typeof(AdminWebhookEventDto),typeof(AdminWebhookDeliveryDto),typeof(AdminCredentialMetadataDto),
            typeof(AdminAuditDto),typeof(RiskDecisionDto)};
        foreach(var property in types.SelectMany(x=>x.GetProperties()))
            Assert.DoesNotContain(forbidden,x=>property.Name.Contains(x,StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Risk_defaults_are_explicitly_disabled()
    {
        var options=new RiskOptions();
        Assert.False(options.TerminalPayments.Enabled);
        Assert.False(options.CustomerPayments.Enabled);
        Assert.False(options.TerminalRefunds.Enabled);
        Assert.False(options.CustomerFundingStarts.Enabled);
    }
}
