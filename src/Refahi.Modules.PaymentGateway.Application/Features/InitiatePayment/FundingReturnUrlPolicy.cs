using System;
using System.Linq;
using Refahi.Modules.PaymentGateway.Application.Contracts.Configuration;

namespace Refahi.Modules.PaymentGateway.Application.Features.InitiatePayment;

internal static class FundingReturnUrlPolicy
{
    public static bool IsAllowed(string? value, FundingSafetyOptions options)
    {
        if (string.IsNullOrWhiteSpace(value))
            return true;
        if (Uri.TryCreate(value, UriKind.Relative, out _) && !value.StartsWith("//"))
            return options.AllowRelativeReturnUrls && value.StartsWith('/');
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri))
            return false;
        if (uri.Scheme is not ("https" or "http"))
            return false;
        if (uri.Scheme == "http" && !uri.IsLoopback)
            return false;
        return options.AllowedReturnHosts.Any(host =>
            string.Equals(host, uri.Host, StringComparison.OrdinalIgnoreCase));
    }
}
