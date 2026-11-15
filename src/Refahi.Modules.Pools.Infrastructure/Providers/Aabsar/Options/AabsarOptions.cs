namespace Refahi.Modules.Pools.Infrastructure.Providers.Aabsar.Options;

public sealed class AabsarOptions
{
    public const string SectionName = "Pools:Providers:Aabsar";
    public const string DefaultBaseUrl = "https://api.aabsar.com/api/outbound/refahi/";

    public bool Enabled { get; init; }
    public bool CatalogueEnabled { get; init; }
    public bool PurchaseEnabled { get; init; }
    public int RecipientRetentionDays { get; init; } = 90;
    public string AccessToken { get; init; } = string.Empty;
    public string BaseUrl { get; init; } = DefaultBaseUrl;
    public int TimeoutSeconds { get; init; } = 15;
    public int CatalogCacheSeconds { get; init; } = 120;
}

