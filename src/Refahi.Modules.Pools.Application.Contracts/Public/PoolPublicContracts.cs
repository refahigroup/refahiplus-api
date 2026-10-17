using MediatR;

namespace Refahi.Modules.Pools.Application.Contracts.Public;

public sealed record PoolPage<T>(IReadOnlyList<T> Items, int Page, int Limit, int Total);
public sealed record PoolVenueDto(Guid Id, string Name, string State, string City, string? Area, string? ImageUrl,
    int Gender, decimal? Rating, long? MinimumSaleMinor, bool IsSelling, DateTimeOffset FetchedAt);
public sealed record PoolSessionDto(Guid Id, Guid PoolId, string PoolName, string State, string City, string? ImageUrl,
    string Name, int Gender, string Date, string Time, DateTimeOffset? StartsAtUtc, DateTimeOffset? EndsAtUtc,
    long AdultMinor, long ChildMinor, bool AdultEnabled, bool ChildEnabled, bool IsSelling, bool CanPurchase,
    string DescriptionHtml, string TermsHtml, DateTimeOffset FetchedAt);
public sealed record PoolVenueDetailsDto(PoolVenueDto Pool, string TermsHtml, IReadOnlyList<PoolSessionDto> Sessions);
public sealed record PoolLocationDto(string Id, string Name);
public sealed record PoolCatalogFilter(string? Search = null, string? State = null, string? City = null, int? Gender = null,
    string Sort = "name", int Page = 1, int Limit = 10, Guid? PoolId = null, DateOnly? Date = null);
public sealed record ListPoolVenuesQuery(PoolCatalogFilter Filter) : IRequest<PoolPage<PoolVenueDto>>;
public sealed record GetPoolVenueQuery(Guid Id) : IRequest<PoolVenueDetailsDto>;
public sealed record ListPoolSessionsQuery(PoolCatalogFilter Filter) : IRequest<PoolPage<PoolSessionDto>>;
public sealed record GetPoolSessionQuery(Guid Id) : IRequest<PoolSessionDto>;
public sealed record GetPoolLocationsQuery(string? State, bool Cities) : IRequest<IReadOnlyList<PoolLocationDto>>;
public sealed record SyncPoolCatalogCommand(bool Venues) : IRequest;

public sealed record PoolChoice(Guid SessionId, int AdultCount, int ChildCount);
public sealed record PoolQuoteLine(Guid SessionId, long TotalMinor);
public sealed record PoolQuoteDto(IReadOnlyList<PoolQuoteLine> Lines, long TotalMinor);
public sealed record QuotePoolPurchaseQuery(IReadOnlyList<PoolChoice> Choices) : IRequest<PoolQuoteDto>;
public sealed record CreatePoolPurchaseCommand(Guid UserId, IReadOnlyList<PoolChoice> Choices, long ExpectedTotalMinor,
    string IdempotencyKey) : IRequest<PoolPurchaseDto>;
public sealed record GetPoolPurchaseQuery(Guid Id, Guid UserId) : IRequest<PoolPurchaseDto>;
public sealed record CheckoutPoolPurchaseCommand(Guid Id, Guid UserId, long Version, long TotalMinor) : IRequest<PoolPurchaseCheckoutDto>;
public sealed record CancelPoolPurchaseCommand(Guid Id, Guid UserId) : IRequest<PoolPurchaseDto>;
public sealed record ReconcilePoolPurchaseCommand(Guid Id) : IRequest;
public sealed record IssuePoolVouchersCommand(Guid Id) : IRequest;
public sealed record PoolPurchaseCheckoutDto(Guid PurchaseId, Guid OrderId, string OrderNumber, long TotalMinor);
public sealed record PoolVoucherDto(Guid RequestId, Guid SessionId, Guid PoolId, string PoolName, string Date, string Time,
    int AdultCount, int ChildCount, long TotalMinor, DateTimeOffset StartsAtUtc, DateTimeOffset EndsAtUtc,
    string ReservationStatus, string? Status, string? Code, DateTimeOffset? RedeemedAtUtc);
public sealed record PoolPurchaseDto(Guid Id, Guid? OrderId, string? OrderNumber, long Version, long TotalMinor,
    string Status, DateTimeOffset PayableUntil, bool CanCheckout, bool CanCancel, IReadOnlyList<PoolVoucherDto> Requests);
public sealed record ValidatePoolVoucherQuery(Guid OperatorId, Guid PoolId, string Code) : IRequest<PoolVoucherValidationDto>;
public sealed record RedeemPoolVoucherCommand(Guid OperatorId, Guid PoolId, string Code, string IdempotencyKey) : IRequest<PoolVoucherValidationDto>;
public sealed record PoolVoucherValidationDto(Guid RequestId, Guid PoolId, string PoolName, string Date, string Time,
    int AdultCount, int ChildCount, string Status, bool CanRedeem, DateTimeOffset? RedeemedAtUtc);
public sealed record PoolOperatorGrantDto(Guid Id, Guid UserId, Guid PoolId);
public sealed record ListPoolOperatorsQuery() : IRequest<IReadOnlyList<PoolOperatorGrantDto>>;
public sealed record GrantPoolOperatorCommand(Guid ActorId, Guid UserId, Guid PoolId) : IRequest<PoolOperatorGrantDto>;
public sealed record RevokePoolOperatorCommand(Guid ActorId, Guid GrantId) : IRequest<bool>;
public sealed record GetOperatorPoolsQuery(Guid UserId) : IRequest<IReadOnlyList<PoolVenueDto>>;
public sealed record DownloadPoolVouchersQuery(Guid Id, Guid UserId) : IRequest<byte[]>;
public sealed record CleanupLegacyPoolsCommand(Guid ActorId) : IRequest<int>;

public interface IPoolCatalogReader
{
    Task<PoolPage<PoolVenueDto>> VenuesAsync(PoolCatalogFilter filter, CancellationToken ct);
    Task<PoolPage<PoolSessionDto>> SessionsAsync(PoolCatalogFilter filter, CancellationToken ct);
    Task<PoolVenueDetailsDto?> VenueAsync(Guid id, CancellationToken ct);
    Task<PoolSessionDto?> SessionAsync(Guid id, CancellationToken ct);
    Task<IReadOnlyList<PoolLocationDto>> LocationsAsync(string? state, bool cities, CancellationToken ct);
}
public interface IPoolCodeProtector
{
    string Protect(string code);
    string Unprotect(string ciphertext);
}
public interface IPoolVoucherRenderer { Task<byte[]> RenderAsync(PoolPurchaseDto purchase, CancellationToken ct); }
public interface IPoolReservationGateway { Task<bool> ReserveAsync(Guid requestId, CancellationToken ct); }
public sealed record LegacyPoolBooking(Guid Id, Guid UserId, Guid? OrderId);
public interface ILegacyPoolStore
{
    Task<IReadOnlyList<LegacyPoolBooking>> ListAsync(CancellationToken ct);
    Task<bool> IsLegacyAsync(Guid id, Guid user, CancellationToken ct);
    Task DeleteAsync(Guid id, CancellationToken ct);
    Task CompleteCleanupAsync(CancellationToken ct);
}

public sealed record PoolCatalogDiagnosticsDto(string Status, int VenueCount, int SessionCount, int StaleVenues, int StaleSessions,
    DateTimeOffset? LastVenueSyncUtc, DateTimeOffset? LastSessionSyncUtc);
public sealed record GetPoolCatalogDiagnosticsQuery() : IRequest<PoolCatalogDiagnosticsDto>;
public interface IPoolCatalogDiagnostics { Task<PoolCatalogDiagnosticsDto> ReadAsync(CancellationToken ct); }
