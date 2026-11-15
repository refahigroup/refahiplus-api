namespace Refahi.Modules.Cinema.Infrastructure.Persistence;

internal sealed class CinemaFilm
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string Title { get; set; } = "";
    public string Kind { get; set; } = "";
    public string[] Aliases { get; set; } = [];
    public string[] Genres { get; set; } = [];
    public string? SummaryText { get; set; }
    public string? SummaryHtml { get; set; }
    public string? DescriptionHtml { get; set; }
    public string? Poster { get; set; }
    public string? Cover { get; set; }
    public int? DurationMinutes { get; set; }
    public string? AgeGroup { get; set; }
    public int? ReleaseYear { get; set; }
    public List<CinemaCredit> Credits { get; } = [];
}

internal sealed class CinemaPerson
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string Name { get; set; } = "";
    public string[] Aliases { get; set; } = [];
    public string? Portrait { get; set; }
}

internal sealed class CinemaCredit
{
    public Guid FilmId { get; init; }
    public Guid PersonId { get; init; }
    public CinemaPerson Person { get; init; } = null!;
    public string? Role { get; set; }
    public int SortOrder { get; set; }
}

internal sealed class CinemaVenue
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string Name { get; set; } = "";
    public string[] Aliases { get; set; } = [];
    public string? Address { get; set; }
    public string? Image { get; set; }
    public int? CityId { get; set; }
    public string? CityName { get; set; }
    public int? ProvinceId { get; set; }
    public string? ProvinceName { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
}

internal sealed class ITicketMapping
{
    public required string Kind { get; init; }
    public required string ExternalId { get; init; }
    public required Guid CanonicalId { get; init; }
}

internal sealed class ITicketGeography
{
    public int ExternalCityId { get; init; }
    public int ExternalProvinceId { get; set; }
    public int? CityId { get; set; }
    public int? ProvinceId { get; set; }
}

internal sealed class ITicketScreening
{
    public required string ExternalId { get; init; }
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid FilmId { get; set; }
    public Guid VenueId { get; set; }
    public string ExternalFilmId { get; set; } = "";
    public string ExternalVenueId { get; set; } = "";
    public DateTimeOffset StartsAt { get; set; }
    public DateTimeOffset? PurchaseEndsAt { get; set; }
    public DateTimeOffset? PurchaseStartsAt { get; set; }
    public string Status { get; set; } = "";
    public bool Published { get; set; }
}

internal sealed class ITicketDiscoveryState
{
    public int Id { get; init; } = 1;
    public DateTimeOffset? LastSuccessAt { get; set; }
    public DateTimeOffset LastAttemptAt { get; set; }
    public bool Failed { get; set; }
    public int UnmappedVenues { get; set; }
    public int PendingMappings { get; set; }
}
