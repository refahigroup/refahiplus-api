namespace Refahi.Modules.Pools.Application.Contracts;

public sealed record AabsarSaleOptions(bool Enabled, int RecipientRetentionDays);
public sealed record AabsarContact(string FullName, string? MobileNumber);
public sealed record AabsarSelection(string ShowtimeId, int AdultCount, int ChildCount);
public sealed record AabsarLiveQuote(int Capacity, long AdultPrice, long ChildPrice, long? AdultOldPrice, long? ChildOldPrice);
public sealed record AabsarTicket(Guid Id, bool IsChild, string? Code);
public sealed record AabsarReceipt(string OrderCode, IReadOnlyList<AabsarTicket> Tickets, long? TotalMinor = null);
public sealed record AabsarCartItemInput(string ProviderKey, string SellerKey, string ProductKey, string OfferKey,
    string PurchaseOptionKey, int Quantity, long ExpectedUnitPriceMinor);
public sealed record AabsarCheckoutInput(string IdempotencyKey, string? CartVersion);
public sealed record AabsarConfirmInput(string Version);
public sealed record AabsarResolution(string Outcome, string Evidence, AabsarReceipt? Receipt = null);
public sealed record AabsarPage<T>(IReadOnlyList<T> Items, int PageNumber, int PageSize, int TotalCount, int TotalPages);
public sealed record AabsarAddress(string State, string City, string Address, AabsarPoint Location);
public sealed record AabsarPoint(double Lat, double Lng);
public sealed record AabsarSeller(string ProviderKey, string SellerKey, string Title, string[] Categories,
    string? LogoUrl, string? CoverUrl, string Description, AabsarAddress Address);
public sealed record AabsarOption(string Key, string Title, long PriceMinor, long? OriginalPriceMinor, bool IsAvailable);
public sealed record AabsarOffer(string ProviderKey, string SellerKey, string ProductKey, string OfferKey, string Title,
    string? Subtitle, DateTimeOffset StartsAt, int Capacity, string CategoryCode, IReadOnlyList<AabsarOption> PurchaseOptions);
public sealed record AabsarProductView(string ProviderKey, string SellerKey, string ProductKey, string Title,
    string Description, string? ImageUrl, string[] Tags, AabsarAddress Address, IReadOnlyList<AabsarOffer> Offers);

public interface IAabsarCatalogue
{
    Task<AabsarSeller?> VenueAsync(CancellationToken ct);
    Task<AabsarPage<AabsarProductView>> ProductsAsync(string? search, int page, int size, CancellationToken ct);
    Task<AabsarProductView?> ProductAsync(string key, CancellationToken ct);
    Task<Guid?> ResolveLegacyProductAsync(string key, CancellationToken ct);
}

public interface IAabsarGateway
{
    AabsarSaleOptions Options { get; }
    Task<AabsarLiveQuote> QuoteAsync(string showtimeId, CancellationToken ct);
    Task<AabsarReceipt?> IssueAsync(AabsarContact contact, IReadOnlyList<AabsarSelection> selections, CancellationToken ct);
    Task<bool> CancelAsync(string orderCode, int adults, int children, CancellationToken ct);
}
