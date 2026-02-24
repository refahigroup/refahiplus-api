namespace Refahi.Modules.Hotels.Infrastructure.Providers.SnappTrip.Contract;

/// <summary>
/// Root response برای /availability/cities
/// {
///   "filter": { ... },
///   "items": [ ... ]
/// }
/// </summary>
public sealed class SnappTripCityAvailabilityResponse
{
    public SnappTripCityAvailabilityFilter filter { get; set; } = new();
    public List<SnappTripCityAvailabilityItem> items { get; set; } = new();
}

/// <summary>
/// همان CityFilter در Swagger
/// </summary>
public sealed class SnappTripCityAvailabilityFilter
{
    public List<int> stars { get; set; } = new();

    /// <summary>
    /// در Swagger: array of string
    /// </summary>
    public List<string> accommodations { get; set; } = new();

    public int min_price { get; set; }
    public int max_price { get; set; }
    public int available_rooms { get; set; }
    public int adults { get; set; }
    public int children { get; set; }
}

/// <summary>
/// آیتم‌های موجود در "items" ریسپانس
/// {
///   "city_id": 7454,
///   "hotel": { ... },
///   "room": { ... }
/// }
/// </summary>
public sealed class SnappTripCityAvailabilityItem
{
    public int city_id { get; set; }
    public SnappTripCityAvailabilityHotel hotel { get; set; } = new();
    public SnappTripCityAvailabilityRoom room { get; set; } = new();
}

/// <summary>
/// hotel در هر آیتم
/// </summary>
public sealed class SnappTripCityAvailabilityHotel
{
    public int id { get; set; }
    public string title { get; set; } = default!;
    public int stars { get; set; }

    public string accommodation_title { get; set; } = default!;
    public string accommodation_type { get; set; } = default!;

    public string address { get; set; } = default!;
}

/// <summary>
/// room در هر آیتم
/// </summary>
public sealed class SnappTripCityAvailabilityRoom
{
    public int id { get; set; }
    public string title { get; set; } = default!;

    public long price { get; set; }
    public long price_off { get; set; }
    public int discount_percent { get; set; }

    public long child_price { get; set; }
    public long extra_bed_price { get; set; }

    /// <summary>
    /// ظرفیت child (طبق نمونه JSON که دادی)
    /// </summary>
    public int children { get; set; }
}
