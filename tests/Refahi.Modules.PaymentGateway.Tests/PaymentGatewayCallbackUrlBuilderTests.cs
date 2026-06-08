using Refahi.Modules.PaymentGateway.Application.Contracts.Features.InitiatePayment;
using Refahi.Modules.PaymentGateway.Domain.Enums;
using Xunit;

namespace Refahi.Modules.PaymentGateway.Tests;

public class PaymentGatewayCallbackUrlBuilderTests
{
    [Fact]
    public void Build_UsesConfiguredPublicBaseUrl_WhenProvided()
    {
        var url = PaymentGatewayCallbackUrlBuilder.Build(
            PaymentGatewayProviderType.Sep,
            "https://refahiplus.com",
            "http",
            "internal-server");

        Assert.Equal("https://refahiplus.com/api/payment-gateway/callback/sep", url);
    }

    [Fact]
    public void Build_UsesForwardedHeaders_WhenPublicBaseUrlIsMissing()
    {
        var url = PaymentGatewayCallbackUrlBuilder.Build(
            PaymentGatewayProviderType.Sep,
            publicBaseUrl: "",
            requestScheme: "http",
            requestHost: "internal-server",
            forwardedProto: "https",
            forwardedHost: "refahiplus.com");

        Assert.Equal("https://refahiplus.com/api/payment-gateway/callback/sep", url);
    }

    [Fact]
    public void Build_ForcesHttpsForPublicHost_WhenRequestSchemeIsHttp()
    {
        var url = PaymentGatewayCallbackUrlBuilder.Build(
            PaymentGatewayProviderType.Sep,
            publicBaseUrl: "",
            requestScheme: "http",
            requestHost: "refahiplus.com");

        Assert.Equal("https://refahiplus.com/api/payment-gateway/callback/sep", url);
    }

    [Fact]
    public void Build_KeepsHttpForLocalhostDevelopment()
    {
        var url = PaymentGatewayCallbackUrlBuilder.Build(
            PaymentGatewayProviderType.Sep,
            publicBaseUrl: "",
            requestScheme: "http",
            requestHost: "localhost:5000");

        Assert.Equal("http://localhost:5000/api/payment-gateway/callback/sep", url);
    }
}
