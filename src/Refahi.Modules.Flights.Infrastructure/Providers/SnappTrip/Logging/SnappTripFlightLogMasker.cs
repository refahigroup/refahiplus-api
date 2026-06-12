using System.Text.RegularExpressions;

namespace Refahi.Modules.Flights.Infrastructure.Providers.SnappTrip.Logging;

internal static partial class SnappTripFlightLogMasker
{
    private const string Mask = "***MASKED***";

    public static string? MaskText(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return value;

        var masked = SensitiveJsonFieldRegex().Replace(value, match =>
            $"{match.Groups["prefix"].Value}{Mask}{match.Groups["suffix"].Value}");

        masked = ApiKeyHeaderRegex().Replace(masked, match =>
            $"{match.Groups["prefix"].Value}{Mask}");

        return masked;
    }

    public static string? MaskToken(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return value;

        if (value.Length <= 8)
            return Mask;

        return $"{value[..4]}...{value[^4..]}";
    }

    [GeneratedRegex(
        "(?<prefix>\"(?:fareSourceCode|nationalId|documentId|phoneNumber|receiverPhoneNumber|phone|email|receiverEmail|number|passportNumber|apiKey|api-key)\"\\s*:\\s*\")(?<value>.*?)(?<suffix>\")",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex SensitiveJsonFieldRegex();

    [GeneratedRegex(
        "(?<prefix>(?:api-key|ApiKey|Authorization)\\s*[:=]\\s*)(?<value>[^\\s,;]+)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ApiKeyHeaderRegex();
}
