namespace Refahi.Modules.Commerce.Application.Contracts.Providers;

public sealed record CommerceQuoteResult(
    string ProviderKey, 
    string SellerKey, 
    string ProductKey, 
    string OfferKey,
    string PurchaseOptionKey, 
    string SellerTitle,
    string ProductTitle, 
    string? ProductImageUrl,
    string OfferTitle, 
    string OptionTitle, 
    string CategoryCode,
    int Quantity, 
    long UnitPriceMinor,
    long OriginalUnitPriceMinor,
    int? Capacity, 
    string ProviderPayloadJson
)
{
    public long? ProviderCostMinor { get; init; }
    public long? MarkupMinor { get; init; }
    public string? PricingVersion { get; init; }
    public bool RequiresManifest { get; init; }
    public bool RequiresIdentityNumber { get; init; }
}
