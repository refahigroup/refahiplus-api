using MediatR;

namespace Refahi.Modules.Pools.Application.Contracts;

public sealed record PoolCapabilities(bool Discovery, bool Quote, bool Reserve, bool Confirm, bool Status, bool Cancel, bool IsMock);
public sealed record PoolProviderInfo(string Key, string Name, PoolCapabilities Capabilities);
public sealed record PoolState(string Id, string Name);
public sealed record PoolCity(string Id, string Name, string StateId, string StateName);
public sealed record PoolSort(string Code, string Title);
public sealed record PoolSummary(string ProviderKey, string Id, string Name, string? ImageUrl, string State, string City,
    string? Area, decimal? Latitude, decimal? Longitude, bool IsSelling, int Gender, decimal? Rating, decimal? MaxDiscount,
    decimal? MinimumSalePrice, decimal? MinimumMarketPrice, string? PriceUnit);
public sealed record PoolCatalog(IReadOnlyList<PoolSummary> Items, int Page, int Limit, IReadOnlyList<PoolSort> Sorts, string? SortedBy);
public sealed record PoolSession(string Id, string Name, int Gender, IReadOnlyList<PoolSessionRow> Rows);
public sealed record PoolSessionRow(string Id, bool IsSelling, bool AdultEnabled, bool ChildEnabled, decimal AdultPrice,
    decimal ChildPrice, decimal AdultMarketPrice, decimal ChildMarketPrice, string Date, string Day, string Time,
    string DescriptionHtml, string TermsHtml, string? PriceUnit);
public sealed record PoolDetails(PoolSummary? Summary, IReadOnlyList<PoolSession> Sessions, string TermsHtml, PoolCapabilities Capabilities);
public sealed record PoolSelection(string ProviderKey, string PoolId, string SessionRowId, int AdultCount, int ChildCount);
public sealed record PoolQuote(string RowId, int AdultCount, int ChildCount, decimal SaleTotal, decimal MarketTotal,
    long? TotalMinor, string? PriceUnit, string Date, string Day, string Time);
public sealed record PoolReservation(string Reference, string Status, DateTimeOffset ExpiresAt, long TotalMinor, string? TicketCode, bool IsMock);
public sealed record PoolBookingView(Guid Id, Guid? OrderId, string? OrderNumber, long Version, PoolSelection Selection,
    PoolSummary? Pool, PoolSessionRow Row, long TotalMinor, string PaymentStatus, string ReservationStatus, string IssuanceStatus,
    string CancellationStatus, DateTimeOffset? PayableUntil, bool CanCheckout, bool CanCancel, bool IsMock, string? TicketCode, string? Message);
public sealed record PoolCheckout(Guid BookingId, Guid OrderId, string OrderNumber, long TotalMinor);

public sealed record PoolSearchQuery(string ProviderKey, string? StateId = null, string? CityId = null, string? AreaId = null,
    decimal? Latitude = null, decimal? Longitude = null, string? Search = null, int? Gender = null, string? Sort = null,
    int Page = 1, int Limit = 10, bool? ActiveOnly = null) : IRequest<PoolCatalog>;
public sealed record PoolProvidersQuery : IRequest<IReadOnlyList<PoolProviderInfo>>;
public sealed record PoolStatesQuery(string ProviderKey, bool ActiveOnly = true) : IRequest<IReadOnlyList<PoolState>>;
public sealed record PoolCitiesQuery(string ProviderKey, string? StateId, bool ActiveOnly = true) : IRequest<IReadOnlyList<PoolCity>>;
public sealed record PoolDetailsQuery(string ProviderKey, string PoolId) : IRequest<PoolDetails>;
public sealed record PoolSessionsQuery(string ProviderKey, string PoolId) : IRequest<IReadOnlyList<PoolSession>>;
public sealed record PoolTermsQuery(string ProviderKey, string PoolId) : IRequest<string>;
public sealed record PoolQuoteQuery(PoolSelection Selection) : IRequest<PoolQuote>;
public sealed record ReservePoolBookingCommand(Guid UserId, PoolSelection Selection, long ExpectedTotalMinor, string IdempotencyKey) : IRequest<PoolBookingView>;
public sealed record CheckoutPoolBookingCommand(Guid Id, Guid UserId, long Version, long TotalMinor) : IRequest<PoolCheckout>;
public sealed record GetPoolBookingQuery(Guid Id, Guid UserId) : IRequest<PoolBookingView>;
public sealed record CancelPoolBookingCommand(Guid Id, Guid UserId) : IRequest<PoolBookingView>;
public sealed record ReconcilePoolBookingCommand(Guid Id) : IRequest;

public interface IPoolProvider
{
    string Key { get; }
    string Name { get; }
    PoolCapabilities Capabilities { get; }
    Task<IReadOnlyList<PoolState>> StatesAsync(bool activeOnly, CancellationToken ct);
    Task<IReadOnlyList<PoolCity>> CitiesAsync(string? stateId, bool activeOnly, CancellationToken ct);
    Task<PoolCatalog> SearchAsync(PoolSearchQuery query, CancellationToken ct);
    Task<IReadOnlyList<PoolSession>> SessionsAsync(string poolId, CancellationToken ct);
    Task<string> TermsAsync(string poolId, CancellationToken ct);
    Task<PoolQuote> QuoteAsync(PoolSelection selection, CancellationToken ct);
}
public interface IPoolReservationProvider { Task<PoolReservation> ReserveAsync(string operationId, PoolSelection selection, PoolQuote quote, CancellationToken ct); }
public interface IPoolConfirmationProvider { Task<PoolReservation> ConfirmAsync(string reference, string operationId, CancellationToken ct); }
public interface IPoolStatusProvider
{
    Task<PoolReservation?> StatusAsync(string reference, CancellationToken ct);
    Task<PoolReservation?> FindByOperationAsync(string operationId, CancellationToken ct) => Task.FromResult<PoolReservation?>(null);
}
public interface IPoolCancellationProvider { Task<PoolReservation> CancelAsync(string reference, string operationId, CancellationToken ct); }
public interface IPoolProviderFactory
{
    IReadOnlyList<IPoolProvider> Enabled();
    IPoolProvider Get(string key);
}
public interface IPoolMutationLock { Task<IAsyncDisposable> AcquireAsync(Guid id, CancellationToken ct); }
public interface IPoolPurchasePolicy { void EnsureAllowed(bool isMock); }
public interface IPoolCatalogStore
{
    Task<PoolSummary?> GetAsync(string provider, string id, CancellationToken ct);
    Task UpsertAsync(IReadOnlyList<PoolSummary> pools, CancellationToken ct);
}
public sealed class PoolsOptions
{
    public bool CatalogEnabled { get; set; }
    public bool PurchaseEnabled { get; set; }
    public bool MockLifecycleEnabled { get; set; }
    public bool FixtureCatalogEnabled { get; set; }
    public bool TestFundsConfirmed { get; set; }
    public int ReservationMinutes { get; set; } = 10;
    public int SafetySeconds { get; set; } = 60;
    public int WorkerSeconds { get; set; } = 15;
}
public sealed class PoolException(string message, int status = 409) : Exception(message) { public int Status { get; } = status; }
public sealed class PoolAmbiguousException(string message = "نتیجه عملیات تأمین‌کننده مشخص نیست") : Exception(message);
