namespace Refahi.Modules.Entertainments.Application.Contracts.Providers;

public sealed record EntertainmentQuoteResult(
    string ProviderKey, 
    string SupplierKey, 
    string ProgramKey, 
    string OfferKey,
    string PurchaseOptionKey, 
    string SupplierTitle,
    string ProgramTitle, 
    string? ProgramImageUrl,
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
    public long DiscountMinor { get; init; }
    public long ProviderRetailAdjustmentMinor { get; init; }
    public IReadOnlyList<string> PreviousPurchaseOptionKeys { get; init; } = [];
    public string? PricingVersion { get; init; }
    public bool RequiresManifest { get; init; }
    public bool RequiresIdentityNumber { get; init; }
}
