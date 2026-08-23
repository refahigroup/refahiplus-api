namespace Refahi.Modules.Commerce.Infrastructure.Providers.Asbsar.Options;

public sealed class AabsarOptions
{
    public const string SectionName = "Commerce:Providers:Aabsar";
    public const string DefaultBaseUrl = "https://api.aabsar.com/api/outbound/refahi/";

    public string AccessToken { get; init; } = string.Empty;
    public string BaseUrl { get; init; } = DefaultBaseUrl;
}

