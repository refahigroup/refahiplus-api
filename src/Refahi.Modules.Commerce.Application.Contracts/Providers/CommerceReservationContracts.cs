using Refahi.Modules.Commerce.Application.Contracts.Providers.Dtos;

namespace Refahi.Modules.Commerce.Application.Contracts.Providers;

public sealed record CommercePassenger(string Name, string? IdentityNumber);
public sealed record CommerceSessionLine(Guid CartItemId, string ProductKey, string OfferKey, string PurchaseOptionKey,
    int Quantity, long ExpectedUnitPriceMinor, IReadOnlyList<CommercePassenger> Passengers);
public sealed record CommerceReservationRequest(string OperationId, string RecipientName, string RecipientMobile,
    IReadOnlyList<CommerceSessionLine> Lines);
public sealed record CommerceReservedLine(Guid CartItemId, CommerceQuoteResult Quote, IReadOnlyList<CommercePassenger> Passengers);
public sealed record CommerceReservationResult(string Reference, DateTimeOffset PayableUntil,
    IReadOnlyList<CommerceReservedLine> Lines, string ProtectedContext);
public interface ICommerceReservationProvider
{
    Task<CommerceReservationResult> ReserveAsync(CommerceReservationRequest request, CancellationToken ct);
    Task ReleaseAsync(string reference, CancellationToken ct);
}
public interface ICommerceFulfillmentStatusProvider
{
    Task<CommerceFulfillmentResult> GetStatusAsync(string operationId, string? invoiceId, CancellationToken ct);
}
public interface ICommerceOfferProvider
{
    Task<IReadOnlyList<CommerceOfferDto>> GetOffersAsync(string productKey, DateOnly? start, DateOnly? end, CancellationToken ct);
    Task<CommerceCatalogFilters> GetFiltersAsync(CancellationToken ct);
}
public sealed record CommerceFilterOption(string Key, string Title);
public sealed record CommerceCatalogFilters(IReadOnlyList<CommerceFilterOption> Locations, IReadOnlyList<CommerceFilterOption> Categories);
public sealed record CommerceDeliveryDocument(string Title, string Url);
public interface ICommercePricingService
{
    CommercePrice Calculate(decimal providerCost, decimal percent, long fixedMinor, string version);
}
public sealed record CommercePrice(long ProviderCostMinor, long MarkupMinor, long SaleMinor, string Version);
public interface ICommerceMutationLock
{
    Task<IAsyncDisposable> AcquireAsync(Guid id, CancellationToken ct);
}
