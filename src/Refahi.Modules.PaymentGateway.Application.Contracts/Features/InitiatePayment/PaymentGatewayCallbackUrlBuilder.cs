using Refahi.Modules.PaymentGateway.Domain.Enums;
using System;

namespace Refahi.Modules.PaymentGateway.Application.Contracts.Features.InitiatePayment;

public static class PaymentGatewayCallbackUrlBuilder
{
    private const string CallbackPathPrefix = "/api/payment-gateway/callback";

    public static string Build(
        PaymentGatewayProviderType provider,
        string? publicBaseUrl,
        string requestScheme,
        string requestHost,
        string? forwardedProto = null,
        string? forwardedHost = null)
    {
        var providerSlug = provider.ToString().ToLowerInvariant();
        var baseUrl = ResolveBaseUrl(publicBaseUrl, requestScheme, requestHost, forwardedProto, forwardedHost);
        return $"{baseUrl.TrimEnd('/')}{CallbackPathPrefix}/{providerSlug}";
    }

    private static string ResolveBaseUrl(
        string? publicBaseUrl,
        string requestScheme,
        string requestHost,
        string? forwardedProto,
        string? forwardedHost)
    {
        if (!string.IsNullOrWhiteSpace(publicBaseUrl))
            return publicBaseUrl.Trim();

        var host = FirstHeaderValue(forwardedHost);
        if (string.IsNullOrWhiteSpace(host))
            host = requestHost;

        var scheme = FirstHeaderValue(forwardedProto);
        if (string.IsNullOrWhiteSpace(scheme))
            scheme = requestScheme;

        if (scheme.Equals("http", StringComparison.OrdinalIgnoreCase) && !IsLocalHost(host))
            scheme = "https";

        return $"{scheme}://{host}";
    }

    private static string FirstHeaderValue(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        var commaIndex = value.IndexOf(',');
        return (commaIndex >= 0 ? value[..commaIndex] : value).Trim();
    }

    private static bool IsLocalHost(string host)
    {
        var hostWithoutPort = host.Split(':', 2)[0];
        return hostWithoutPort.Equals("localhost", StringComparison.OrdinalIgnoreCase) ||
               hostWithoutPort.Equals("127.0.0.1", StringComparison.OrdinalIgnoreCase) ||
               hostWithoutPort.Equals("::1", StringComparison.OrdinalIgnoreCase);
    }
}
