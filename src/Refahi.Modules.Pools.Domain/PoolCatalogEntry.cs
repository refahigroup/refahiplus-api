namespace Refahi.Modules.Pools.Domain;

public sealed class PoolCatalogEntry
{
    private PoolCatalogEntry() { }
    public Guid Id { get; private set; }
    public string ProviderKey { get; private set; } = "";
    public string ExternalId { get; private set; } = "";
    public string Name { get; private set; } = "";
    public string State { get; private set; } = "";
    public string City { get; private set; } = "";
    public string? Area { get; private set; }
    public string? ImageUrl { get; private set; }
    public int Gender { get; private set; }
    public decimal? Rating { get; private set; }
    public long? MinimumSaleMinor { get; private set; }
    public bool IsSelling { get; private set; }
    public bool IsActive { get; private set; }
    public string TermsHtml { get; private set; } = "";
    public DateTimeOffset FetchedAt { get; private set; }
    public DateTimeOffset? SessionsFetchedAt { get; private set; }
    public static PoolCatalogEntry Create(string provider, string externalId) => new() { Id = Guid.NewGuid(), ProviderKey = provider, ExternalId = externalId };
    public void Refresh(string name, string state, string city, string? area, string? image, int gender, decimal? rating,
        long? minimum, bool selling, DateTimeOffset now)
    { Name = name; State = state; City = city; Area = area; ImageUrl = image; Gender = gender; Rating = rating;
        MinimumSaleMinor = minimum; IsSelling = selling; IsActive = true; FetchedAt = now; }
    public void RefreshSessions(string terms, DateTimeOffset now) { TermsHtml = terms; SessionsFetchedAt = now; }
    public void Deactivate() { IsActive = false; IsSelling = false; }
}

public sealed class PoolSessionEntry
{
    private PoolSessionEntry() { }
    public Guid Id { get; private set; }
    public Guid PoolId { get; private set; }
    public string ExternalGroupId { get; private set; } = "";
    public string ExternalRowId { get; private set; } = "";
    public string Name { get; private set; } = "";
    public int Gender { get; private set; }
    public string Date { get; private set; } = "";
    public string Time { get; private set; } = "";
    public DateTimeOffset? StartsAtUtc { get; private set; }
    public DateTimeOffset? EndsAtUtc { get; private set; }
    public long AdultMinor { get; private set; }
    public long ChildMinor { get; private set; }
    public bool AdultEnabled { get; private set; }
    public bool ChildEnabled { get; private set; }
    public bool IsSelling { get; private set; }
    public bool IsActive { get; private set; }
    public string DescriptionHtml { get; private set; } = "";
    public string TermsHtml { get; private set; } = "";
    public DateTimeOffset FetchedAt { get; private set; }
    public static PoolSessionEntry Create(Guid poolId, string groupId, string rowId) => new() { Id = Guid.NewGuid(), PoolId = poolId, ExternalGroupId = groupId, ExternalRowId = rowId };
    public void Refresh(string name, int gender, string date, string time, DateTimeOffset? starts, DateTimeOffset? ends,
        long adult, long child, bool adultEnabled, bool childEnabled, bool selling, string description, string terms, DateTimeOffset now)
    { Name = name; Gender = gender; Date = date; Time = time; StartsAtUtc = starts; EndsAtUtc = ends;
        AdultMinor = adult; ChildMinor = child; AdultEnabled = adultEnabled; ChildEnabled = childEnabled;
        IsSelling = selling; IsActive = true; DescriptionHtml = description; TermsHtml = terms; FetchedAt = now; }
    public void Deactivate() { IsActive = false; IsSelling = false; }
}
