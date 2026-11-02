namespace Refahi.Modules.Hotels.Application.Contracts.Catalogue;

public sealed class HotelStaticContentOptions
{
    public bool WorkerEnabled { get; set; }
    public bool ReadEnabled { get; set; }
    // Explicit rollout scopes: provider name + canonical reference city ID, e.g. SnappTrip:1.
    public string[] ReadScopes { get; set; } = [];
    public string[] SyncScopes { get; set; } = [];
    public int RefreshHours { get; set; } = 24;
    public int RetryMinutes { get; set; } = 15;
    public int RequestTimeoutSeconds { get; set; } = 30;
    // Empty until the supplier's publication statuses have been verified against its contract.
    public string[] SnappTripPublishedReviewStatuses { get; set; } = [];
    public bool Includes(string provider, int city) => ReadEnabled && ReadScopes.Contains($"{provider}:{city}", StringComparer.OrdinalIgnoreCase);
}

public sealed record HotelFacility(string Title, string? Icon = null, string? Id = null, string? Group = null);
public sealed record HotelChildPolicy(decimal From, decimal To, string Type, bool RequiresBed);
public sealed record HotelStaticPolicies(string? CheckInTime = null, string? CheckOutTime = null,
    int? ChildAge = null, int? InfantAge = null, bool? ForeignersFee = null, string? Cancellation = null,
    IReadOnlyList<HotelChildPolicy>? ChildPolicies = null);
public sealed record HotelStaticRoom(string Id, string Title, string Description, int? Adults, int? Children,
    int? ExtraBeds, string? BoardType, IReadOnlyList<HotelFacility> Facilities, IReadOnlyList<Guid>? ImageIds = null, int? Capacity = null,
    int? MaxChildrenWithoutBed = null, string? AccommodationType = null);
public sealed record HotelStaticImage(Guid MediaId, string Title, string Description, bool IsCover, string? SourceUrl = null);
public sealed record HotelStaticReview(string Id, string DisplayName, string Text, double Rating,
    long RegisteredAt, long UpdatedAt, bool Recommended, bool HasBooked, IReadOnlyDictionary<string, double> Scores, string? PublicationStatus = null);
public sealed record HotelStaticRatePlan(string Id, string MealType, string? AgencyProviderId);
public sealed record HotelContentSection(string Status, DateTimeOffset? LastSuccessAtUtc,
    DateTimeOffset NextAttemptAtUtc, string? ErrorCode = null);
public sealed record HotelStaticContent(string? EnglishName, string? AccommodationType, string? AccommodationTitle,
    HotelStaticPolicies Policies, IReadOnlyList<HotelFacility> Facilities, IReadOnlyList<HotelStaticRoom> Rooms,
    IReadOnlyList<HotelStaticImage> Gallery, IReadOnlyList<HotelStaticReview> Reviews, double? Rating, int? ReviewCount,
    IReadOnlyDictionary<string, HotelContentSection> Sections, int Version = 1, IReadOnlyList<HotelStaticRatePlan>? RatePlans = null)
{
    public bool IsReady => new[] { "profile", "facilities", "rooms", "gallery", "reviews" }.All(key =>
        Sections.TryGetValue(key, out var section) && section.Status is "ready" or "unsupported")
        && Gallery.All(x => x.MediaId != Guid.Empty);
}
public sealed record HotelLocalSearch(string? Query = null, int? CityId = null, int? ProvinceId = null,
    int[]? Stars = null, string[]? Accommodations = null, string[]? Facilities = null, double? MinRating = null,
    double? Latitude = null, double? Longitude = null, string Sort = "title", int Page = 1, int PageSize = 20);
public sealed record HotelLocalSearchPage(IReadOnlyList<HotelDiscoveryProfile> Items, int TotalCount, int Page, int PageSize);
public sealed record HotelReviewsPage(IReadOnlyList<HotelStaticReview> Items, int TotalCount, int Page, int PageSize, string Status);
public sealed record HotelContentReadiness(string Provider, int ReferenceCityId, int Hotels, int ReadyHotels, int MissingImages, bool Ready);
public interface IHotelLocalContentReader
{
    Task<Guid?> ResolveLegacyKeyAsync(long snappTripHotelId, CancellationToken ct);
    Task<bool> UsesLocalAsync(Guid hotelKey, CancellationToken ct);
    Task<HotelDiscoveryProfile?> ProfileAsync(Guid hotelKey, CancellationToken ct);
    Task<HotelDiscoveryProfile?> LegacyProfileAsync(long snappTripHotelId, CancellationToken ct);
    Task<HotelLocalSearchPage> SearchAsync(HotelLocalSearch query, CancellationToken ct);
    Task<HotelReviewsPage?> ReviewsAsync(Guid hotelKey, int page, int pageSize, CancellationToken ct);
    Task<IReadOnlyList<HotelContentReadiness>> ReadinessAsync(CancellationToken ct);
    Task<IReadOnlyList<HotelDiscoveryProfile>?> ProviderHotelsAsync(string provider, string nativeCityId, CancellationToken ct);
}
