namespace Refahi.Modules.Pool.Infrastructure.Providers.Aabsar.Options;

/// <summary>
/// Configuration section expected in appsettings.json:
///
/// "AabsarRefahiApi": {
///   "AccessToken": "YOUR_ACCESS_TOKEN",
///   "BaseUrl": "https://api.aabsar.com/api/outbound/refahi/"
/// }
///
/// BaseUrl is optional because the documented production URL is used by default.
/// AccessToken is required and is sent with the exact header name: access-token.
/// </summary>
public sealed class AabsarOptions
{
    public const string SectionName = "Pool.AabsarOptions";
    public const string DefaultBaseUrl = "https://api.aabsar.com/api/outbound/refahi/";

    public string AccessToken { get; init; } = string.Empty;
    public string BaseUrl { get; init; } = DefaultBaseUrl;
}

