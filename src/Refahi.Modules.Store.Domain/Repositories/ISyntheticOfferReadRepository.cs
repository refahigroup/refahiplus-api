namespace Refahi.Modules.Store.Domain.Repositories;

public interface ISyntheticOfferReadRepository
{
    Task<(IReadOnlyList<SyntheticOfferReadModel> Items, int Total)> GetOffersAsync(
        SyntheticOfferQuerySpec spec,
        CancellationToken ct = default);

    Task<(IReadOnlyList<SyntheticProductCatalogReadModel> Items, int Total)> GetProductCatalogAsync(
        SyntheticOfferQuerySpec spec,
        CancellationToken ct = default);

    Task<IReadOnlyList<SyntheticOfferReadModel>> GetProductOffersAsync(
        SyntheticOfferQuerySpec spec,
        CancellationToken ct = default);
}

public sealed record SyntheticOfferQuerySpec(
    IReadOnlyList<Guid> StockBasedAgreementProductIds,
    IReadOnlyList<Guid> SessionBasedAgreementProductIds,
    DateOnly Today,
    string? SearchQuery = null,
    Guid? ShopId = null,
    Guid? ProductId = null,
    string? ProductSlug = null,
    string? OfferKind = null,
    DateOnly? UsageFrom = null,
    DateOnly? UsageTo = null,
    long? MinPriceMinor = null,
    long? MaxPriceMinor = null,
    string Sort = "newest",
    int PageNumber = 1,
    int PageSize = 30,
    TimeOnly? CurrentTime = null);

public sealed class SyntheticOfferReadModel
{
    public string OfferKey { get; set; } = string.Empty;
    public string OfferKind { get; set; } = string.Empty;
    public Guid ProductId { get; set; }
    public Guid AgreementProductId { get; set; }
    public string ProductTitle { get; set; } = string.Empty;
    public string ProductSlug { get; set; } = string.Empty;
    public DateTime ProductCreatedAt { get; set; }
    public Guid ShopId { get; set; }
    public string ShopName { get; set; } = string.Empty;
    public string ShopSlug { get; set; } = string.Empty;
    public Guid? VariantId { get; set; }
    public string? VariantLabel { get; set; }
    public Guid? SessionId { get; set; }
    public DateOnly? SessionDate { get; set; }
    public TimeOnly? SessionStartTime { get; set; }
    public TimeOnly? SessionEndTime { get; set; }
    public string? SessionTitle { get; set; }
    public long OriginalPriceMinor { get; set; }
    public long? DiscountedPriceMinor { get; set; }
    public long EffectivePriceMinor { get; set; }
    public int? AvailableStock { get; set; }
    public int? ConfiguredCapacity { get; set; }
    public bool RequiresUsageDate { get; set; }
    public DateOnly? FromDate { get; set; }
    public DateOnly? ToDate { get; set; }
    public DateOnly? FixedUsageDate { get; set; }
    public string? ImageUrl { get; set; }
    public bool HasVariants { get; set; }
    public bool HasSessions { get; set; }
}

public sealed class SyntheticProductCatalogReadModel
{
    public Guid ProductId { get; set; }
    public Guid AgreementProductId { get; set; }
    public string ProductTitle { get; set; } = string.Empty;
    public string ProductSlug { get; set; } = string.Empty;
    public DateTime ProductCreatedAt { get; set; }
    public string? ImageUrl { get; set; }
    public long MinEffectivePriceMinor { get; set; }
    public long MaxEffectivePriceMinor { get; set; }
    public long DefaultOriginalPriceMinor { get; set; }
    public long? DefaultDiscountedPriceMinor { get; set; }
    public long DefaultEffectivePriceMinor { get; set; }
    public int OfferCount { get; set; }
    public bool HasVariants { get; set; }
    public bool HasSessions { get; set; }
    public string DefaultOfferKey { get; set; } = string.Empty;
    public Guid DefaultShopId { get; set; }
    public string DefaultShopSlug { get; set; } = string.Empty;
}
