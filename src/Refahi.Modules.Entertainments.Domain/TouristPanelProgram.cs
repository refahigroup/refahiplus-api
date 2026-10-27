namespace Refahi.Modules.Entertainments.Domain;

public sealed class TouristPanelProgram
{
    private TouristPanelProgram() { }
    public Guid ProgramKey { get; private set; }
    public string AccountKey { get; private set; } = "";
    public string NativeId { get; private set; } = "";
    public string SupplierId { get; private set; } = "";
    public string SupplierTitle { get; private set; } = "";
    public string Title { get; private set; } = "";
    public string? Description { get; private set; }
    public string? Rules { get; private set; }
    public string? Address { get; private set; }
    public string? LocationCode { get; private set; }
    public string? NativeCityName { get; private set; }
    public int? CategoryCode { get; private set; }
    public string? CategoryTitle { get; private set; }
    public int ProgramType { get; private set; }
    public bool RequiresManifest { get; private set; }
    public bool RequiresIdentityNumber { get; private set; }
    public string[] Images { get; private set; } = [];
    public string SearchText { get; private set; } = "";
    public bool IsListed { get; private set; }
    public DateTimeOffset ListedAtUtc { get; private set; }
    public DateTimeOffset? ObservedAtUtc { get; private set; }
    public DateTimeOffset NextDetailAttemptUtc { get; private set; }
    public static TouristPanelProgram Create(string account, string nativeId) => new()
    { ProgramKey = Guid.NewGuid(), AccountKey = account, NativeId = nativeId };
    public void List(string supplier, string supplierTitle, string title, int type, bool active,
        string? location, string? city, int? category, string? categoryTitle, DateTimeOffset now)
    {
        SupplierId = supplier; SupplierTitle = supplierTitle; if (ObservedAtUtc is null) Title = title; ProgramType = type;
        IsListed = active && type is 1 or 2; LocationCode = location; NativeCityName = city;
        CategoryCode = category; CategoryTitle = categoryTitle; ListedAtUtc = now;
    }
    public void Publish(string title, string? description, string? rules, string? address,
        bool manifest, bool identityNumber, string[] images, string searchText, DateTimeOffset now, int refreshMinutes)
    {
        Title = title; Description = description; Rules = rules; Address = address;
        RequiresManifest = manifest; RequiresIdentityNumber = identityNumber; Images = images;
        SearchText = searchText; ObservedAtUtc = now; NextDetailAttemptUtc = now.AddMinutes(refreshMinutes);
    }
    public void Retire() => IsListed = false;
    public void DetailFailed(DateTimeOffset now) => NextDetailAttemptUtc = now.AddMinutes(15);
}

public sealed class TouristPanelSyncState
{
    private TouristPanelSyncState() { }
    public string AccountKey { get; private set; } = "";
    public DateTimeOffset? LastSuccessUtc { get; private set; }
    public DateTimeOffset NextAttemptUtc { get; private set; }
    public string Status { get; private set; } = "Pending";
    public static TouristPanelSyncState Create(string account) => new() { AccountKey = account };
    public void Success(DateTimeOffset now, int refresh) { LastSuccessUtc = now; NextAttemptUtc = now.AddMinutes(refresh); Status = "Ready"; }
    public void Failed(DateTimeOffset now) { NextAttemptUtc = now.AddMinutes(15); Status = "Failed"; }
}

public sealed class TouristPanelLocationMapping
{
    private TouristPanelLocationMapping() { }
    public string AccountKey { get; private set; } = "";
    public string LocationCode { get; private set; } = "";
    public int CityId { get; private set; }
    public Guid UpdatedBy { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }
    public static TouristPanelLocationMapping Create(string account, string location, int city, Guid actor)
    { var row = new TouristPanelLocationMapping { AccountKey = account, LocationCode = location }; row.Update(city, actor); return row; }
    public void Update(int city, Guid actor) { CityId = city; UpdatedBy = actor; UpdatedAtUtc = DateTimeOffset.UtcNow; }
}
