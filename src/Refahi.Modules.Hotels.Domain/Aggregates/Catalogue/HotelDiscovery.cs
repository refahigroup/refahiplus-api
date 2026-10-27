namespace Refahi.Modules.Hotels.Domain.Aggregates.Catalogue;

public abstract class HotelDiscovery
{
    protected HotelDiscovery() { }
    protected void Initialize(Guid key, string id, string city) { HotelKey = key; NativeId = id; NativeCityId = city; }
    public void Retire() => Published = false;
    public void Publish(int city, string title, string description, string address, int stars, string images, string facilities, string? policies, string search, string sort, DateTimeOffset observed, double? latitude = null, double? longitude = null)
    {
        if (string.IsNullOrWhiteSpace(title) || stars is < 0 or > 5) throw new ArgumentException("اطلاعات هتل معتبر نیست.");
        var valid = latitude.HasValue && longitude.HasValue && double.IsFinite(latitude.Value) && double.IsFinite(longitude.Value) && latitude is >= -90 and <= 90 && longitude is >= -180 and <= 180;
        Latitude = valid ? latitude : null; Longitude = valid ? longitude : null;
        ReferenceCityId = city; Title = title; Description = description; Address = address; Stars = stars; ImagesJson = images; FacilitiesJson = facilities; Policies = policies; SearchText = search; SortTitle = sort; ObservedAtUtc = observed; Published = true;
    }
    public double? Latitude { get; private set; }
    public double? Longitude { get; private set; }
    public Guid HotelKey { get; private set; }
    public string NativeId { get; private set; } = "";
    public string NativeCityId { get; private set; } = "";
    public int ReferenceCityId { get; private set; }
    public string Title { get; private set; } = "";
    public string Description { get; private set; } = "";
    public string Address { get; private set; } = "";
    public int Stars { get; private set; }
    public string ImagesJson { get; private set; } = "[]";
    public string FacilitiesJson { get; private set; } = "[]";
    public string? Policies { get; private set; }
    public string SearchText { get; private set; } = "";
    public string SortTitle { get; private set; } = "";
    public bool Published { get; private set; }
    public DateTimeOffset ObservedAtUtc { get; private set; }
    public string? ContentJson { get; private set; }
    public string? AccommodationType { get; private set; }
    public double? Rating { get; private set; }
    public string[]? FacilityKeys { get; private set; }
    public bool? ContentReady { get; private set; }
    public void UpdateContent(string json, string? accommodationType, double? rating, string[] facilityKeys, bool ready)
    {
        ContentJson = json; AccommodationType = accommodationType; Rating = rating;
        FacilityKeys = facilityKeys; ContentReady = ready;
    }
}
public abstract class HotelDiscoverySyncState
{
    protected HotelDiscoverySyncState() { }
    protected void Initialize(string city) => NativeCityId = city;
    public void Succeed(DateTimeOffset now, int hours) { LastSuccessAtUtc = now; NextAttemptAtUtc = now.AddHours(hours); Status = "ready"; }
    public void Fail(DateTimeOffset now, int minutes) { NextAttemptAtUtc = now.AddMinutes(minutes); Status = "failed"; }
    public string NativeCityId { get; private set; } = "";
    public DateTimeOffset? LastSuccessAtUtc { get; private set; }
    public DateTimeOffset NextAttemptAtUtc { get; private set; }
    public string Status { get; private set; } = "pending";
    public DateTimeOffset? NextContentAttemptAtUtc { get; private set; }
    public DateTimeOffset? LastContentSuccessAtUtc { get; private set; }
    public void ScheduleContent(DateTimeOffset now, bool success, int refreshHours, int retryMinutes)
    {
        NextContentAttemptAtUtc = success ? now.AddHours(refreshHours) : now.AddMinutes(retryMinutes);
        if (success) LastContentSuccessAtUtc = now;
    }
}
public sealed class SnappTripDiscoveryHotel : HotelDiscovery
{
    private SnappTripDiscoveryHotel() { }
    public static SnappTripDiscoveryHotel Create(Guid key, string id, string city) { var row = new SnappTripDiscoveryHotel(); row.Initialize(key, id, city); return row; }
}
public sealed class SnappTripDiscoverySyncState : HotelDiscoverySyncState
{
    private SnappTripDiscoverySyncState() { }
    public static SnappTripDiscoverySyncState Create(string city) { var row = new SnappTripDiscoverySyncState(); row.Initialize(city); return row; }
}
public sealed class LamasoDiscoveryHotel : HotelDiscovery
{
    private LamasoDiscoveryHotel() { }
    public static LamasoDiscoveryHotel Create(Guid key, string id, string city) { var row = new LamasoDiscoveryHotel(); row.Initialize(key, id, city); return row; }
}
public sealed class LamasoDiscoverySyncState : HotelDiscoverySyncState
{
    private LamasoDiscoverySyncState() { }
    public static LamasoDiscoverySyncState Create(string city) { var row = new LamasoDiscoverySyncState(); row.Initialize(city); return row; }
}
