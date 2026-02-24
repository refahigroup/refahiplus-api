namespace Refahi.Modules.Hotels.Infrastructure.Providers.SnappTrip.Contract;

/// <summary>
/// Request body برای /availability/cities مطابق Swagger و نمونه‌هایی که تست کردی.
/// </summary>
public sealed class SnappTripCityAvailabilityRequest
{

    public int city_id { get; set; }

    /// <summary>
    /// Check-in به فرمت YYYY-MM-DD
    /// </summary>
    public string checkin { get; set; } = default!;

    /// <summary>
    /// Check-out به فرمت YYYY-MM-DD
    /// </summary>
    public string checkout { get; set; } = default!;

    public int adults { get; set; }
    public int children { get; set; }

    /// <summary>
    /// تعداد اتاق‌های مورد نیاز
    /// </summary>
    public int available_rooms { get; set; }

    /// <summary>
    /// حداقل قیمت (تومان). ۰ یعنی بدون حد پایین.
    /// </summary>
    public int min_price { get; set; }

    /// <summary>
    /// حداکثر قیمت (تومان). ۰ یعنی بدون حد بالا.
    /// </summary>
    public int max_price { get; set; }

    /// <summary>
    /// فیلتر تعداد ستاره‌ها. خالی = بدون فیلتر.
    /// </summary>
    public List<int> stars { get; set; } = new();

    /// <summary>
    /// فیلتر نوع اقامتگاه (Hotel, Apartments, Guesthouse, ...).
    /// در Swagger به‌صورت آرایه‌ی string تعریف شده.
    /// خالی = بدون فیلتر.
    /// </summary>
    public List<string> accommodations { get; set; } = new();
}
