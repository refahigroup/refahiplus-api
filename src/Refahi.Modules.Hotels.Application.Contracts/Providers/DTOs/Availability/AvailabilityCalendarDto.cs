namespace Refahi.Modules.Hotels.Application.Contracts.Providers.DTOs.Availability;

/// <summary>
/// نشان‌دهنده تقویم دسترسی هتل برای یک بازه زمانی
/// </summary>
public sealed class AvailabilityCalendarDto
{
    /// <summary>
    /// شناسه هتل
    /// </summary>
    public long HotelId { get; set; }

    /// <summary>
    /// نام هتل
    /// </summary>
    public string HotelName { get; set; } = string.Empty;

    /// <summary>
    /// تاریخ شروع
    /// </summary>
    public DateOnly FromDate { get; set; }

    /// <summary>
    /// تاریخ پایان
    /// </summary>
    public DateOnly ToDate { get; set; }

    /// <summary>
    /// اطلاعات اتاق‌ها و دسترسی آن‌ها برای هر روز
    /// Key: RoomTypeId, Value: دسترسی روزانه
    /// </summary>
    public Dictionary<long, List<DailyAvailabilityDto>> RoomCalendars { get; set; } = new();
}

/// <summary>
/// نشان‌دهنده دسترسی یک اتاق برای یک روز معین
/// </summary>
public sealed class DailyAvailabilityDto
{
    /// <summary>
    /// تاریخ
    /// </summary>
    public DateOnly Date { get; set; }

    /// <summary>
    /// آیا اتاق دسترس است؟
    /// </summary>
    public bool IsAvailable { get; set; }

    /// <summary>
    /// قیمت بر شب (اگر دسترس باشد)
    /// </summary>
    public long? PricePerNight { get; set; }

    /// <summary>
    /// تعداد اتاق‌های باقی‌مانده
    /// </summary>
    public int? RemainingRooms { get; set; }

    /// <summary>
    /// دلیل عدم دسترسی (اگر موجود نباشد)
    /// </summary>
    public string? UnavailabilityReason { get; set; }
}
