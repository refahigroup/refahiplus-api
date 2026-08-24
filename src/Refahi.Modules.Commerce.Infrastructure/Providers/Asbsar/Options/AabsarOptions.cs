namespace Refahi.Modules.Commerce.Infrastructure.Providers.Asbsar.Options;

public sealed class AabsarOptions
{
    public const string SectionName = "Commerce:Providers:Aabsar";
    public const string DefaultBaseUrl = "https://api.aabsar.com/api/outbound/refahi/";

    public bool Enabled { get; init; }
    public string AccessToken { get; init; } = string.Empty;
    public string BaseUrl { get; init; } = DefaultBaseUrl;
    public int TimeoutSeconds { get; init; } = 15;
    public int CatalogCacheSeconds { get; init; } = 120;
}

