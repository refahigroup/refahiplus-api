using Refahi.Modules.Entertainments.Application.Contracts.Providers.Dtos;

namespace Refahi.Modules.Entertainments.Application.Contracts.Providers;

public sealed record EntertainmentPassenger(string Name, string? IdentityNumber);
public sealed record EntertainmentSessionLine(Guid CartItemId, string ProgramKey, string OfferKey, string PurchaseOptionKey,
    int Quantity, long ExpectedUnitPriceMinor, IReadOnlyList<EntertainmentPassenger> Passengers);
public sealed record EntertainmentReservationRequest(string OperationId, string RecipientName, string RecipientMobile,
    IReadOnlyList<EntertainmentSessionLine> Lines);
public sealed record EntertainmentReservedLine(Guid CartItemId, EntertainmentQuoteResult Quote, IReadOnlyList<EntertainmentPassenger> Passengers);
public sealed record EntertainmentReservationResult(string Reference, DateTimeOffset PayableUntil,
    IReadOnlyList<EntertainmentReservedLine> Lines, string ProtectedContext);
public interface IEntertainmentReservationProvider
{
    Task<EntertainmentReservationResult> ReserveAsync(EntertainmentReservationRequest request, CancellationToken ct);
    Task ReleaseAsync(string reference, CancellationToken ct);
}
public interface IEntertainmentFulfillmentStatusProvider
{
    Task<EntertainmentFulfillmentResult> GetStatusAsync(string operationId, string? invoiceId, CancellationToken ct);
}
public sealed record EntertainmentPurchaseRequirements(bool RequiresManifest, bool RequiresIdentityNumber);
public interface IEntertainmentOfferProvider
{
    Task<EntertainmentPurchaseRequirements> GetRequirementsAsync(string programKey, CancellationToken ct);
    Task<IReadOnlyList<EntertainmentOfferDto>> GetOffersAsync(string programKey, DateOnly? start, DateOnly? end, CancellationToken ct);
    Task<EntertainmentCatalogFilters> GetFiltersAsync(CancellationToken ct);
}
public sealed record EntertainmentFilterOption(string Key, string Title);
public sealed record EntertainmentCatalogFilters(IReadOnlyList<EntertainmentFilterOption> Locations, IReadOnlyList<EntertainmentFilterOption> Categories);
public sealed record EntertainmentDeliveryDocument(string Title, string Url);
public interface IEntertainmentPricingService
{
    EntertainmentPrice Calculate(decimal providerCost, decimal percent, long fixedMinor, string version);
}
public sealed record EntertainmentPrice(long ProviderCostMinor, long MarkupMinor, long SaleMinor, string Version);
public interface IEntertainmentMutationLock
{
    Task<IAsyncDisposable> AcquireAsync(Guid id, CancellationToken ct);
}
