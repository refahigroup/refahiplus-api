namespace Refahi.Modules.Pools.Domain;

public sealed class PoolTicketGeography
{
    private PoolTicketGeography() { }
    public string CityId { get; private set; } = "";
    public string StateId { get; private set; } = "";
    public string CityName { get; private set; } = "";
    public string StateName { get; private set; } = "";
    public int? ReferenceProvinceId { get; private set; }
    public int? ReferenceCityId { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public static PoolTicketGeography Create(string cityId) => new() { CityId = cityId };
    public void Map(string stateId, string state, string city, int? provinceId, int? cityId, DateTimeOffset now)
    { StateId = stateId; StateName = state; CityName = city; ReferenceProvinceId = provinceId; ReferenceCityId = cityId; UpdatedAt = now; }
}
public sealed class PoolTicketContent
{
    private PoolTicketContent() { }
    public Guid VenueId { get; private set; }
    public string NativeCityId { get; private set; } = "";
    public string NativeStateId { get; private set; } = "";
    public string DescriptionHtml { get; private set; } = "";
    public string Address { get; private set; } = "";
    public string ImagesJson { get; private set; } = "[]";
    public string AttributesJson { get; private set; } = "[]";
    public string? Telephone { get; private set; }
    public double? Latitude { get; private set; }
    public double? Longitude { get; private set; }
    public DateTimeOffset FetchedAt { get; private set; }
    public static PoolTicketContent Create(Guid id) => new() { VenueId = id };
    public void Refresh(string state, string city, string description, string address, string? telephone,
        double? latitude, double? longitude, DateTimeOffset now, string images = "[]", string attributes = "[]")
    { NativeStateId = state; NativeCityId = city; DescriptionHtml = description; Address = address; Telephone = telephone;
      Latitude = latitude; Longitude = longitude; ImagesJson = images; AttributesJson = attributes; FetchedAt = now; }
}
public sealed class PoolTicketSyncState
{
    private PoolTicketSyncState() { }
    public string Key { get; private set; } = "";
    public DateTimeOffset CompletedAt { get; private set; }
    public static PoolTicketSyncState Create() => new() { Key = "venues" };
    public void Complete(DateTimeOffset now) => CompletedAt = now;
}

public sealed class PoolTicketProvince
{
    private PoolTicketProvince() { }
    public string StateId { get; private set; } = "";
    public string StateName { get; private set; } = "";
    public int? ReferenceProvinceId { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public static PoolTicketProvince Create(string id) => new() { StateId = id };
    public void Map(string name, int? provinceId, DateTimeOffset now)
    { StateName = name; ReferenceProvinceId = provinceId; UpdatedAt = now; }
}
