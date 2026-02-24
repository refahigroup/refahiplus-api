namespace Refahi.Modules.Hotels.Application.Contracts.Providers.DTOs.Hotel;

/// <summary>
/// نشان‌دهنده نقدهای هتل
/// </summary>
public sealed class HotelReviewsDto
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
    /// امتیاز کلی (۰-۵)
    /// </summary>
    public decimal OverallRating { get; set; }

    /// <summary>
    /// تعداد نقدها
    /// </summary>
    public int TotalReviews { get; set; }

    /// <summary>
    /// شماره صفحه جاری
    /// </summary>
    public int CurrentPage { get; set; }

    /// <summary>
    /// کل صفحات
    /// </summary>
    public int TotalPages { get; set; }

    /// <summary>
    /// لیست نقدها
    /// </summary>
    public List<ReviewDto> Reviews { get; set; } = new();

    /// <summary>
    /// خلاصه امتیازات
    /// </summary>
    public RatingSummaryDto? RatingSummary { get; set; }
}

/// <summary>
/// یک نقد واحد
/// </summary>
public sealed class ReviewDto
{
    /// <summary>
    /// نام کاربری خاص‌کننده
    /// </summary>
    public string GuestName { get; set; } = string.Empty;

    /// <summary>
    /// تاریخ نقد
    /// </summary>
    public DateTime ReviewDate { get; set; }

    /// <summary>
    /// متن نقد
    /// </summary>
    public string Comment { get; set; } = string.Empty;

    /// <summary>
    /// امتیاز کلی (۰-۵)
    /// </summary>
    public decimal Rating { get; set; }

    /// <summary>
    /// تاریخ اقامت
    /// </summary>
    public DateOnly StayDate { get; set; }

    /// <summary>
    /// تعداد شب‌های اقامت
    /// </summary>
    public int NightsStayed { get; set; }

    /// <summary>
    /// امتیازات تفصیلی
    /// </summary>
    public DetailedRatingsDto? DetailedRatings { get; set; }
}

/// <summary>
/// خلاصه‌ای از امتیازات
/// </summary>
public sealed class RatingSummaryDto
{
    /// <summary>
    /// امتیاز نظافت (۰-۵)
    /// </summary>
    public decimal Cleanliness { get; set; }

    /// <summary>
    /// امتیاز کمفورت (۰-۵)
    /// </summary>
    public decimal Comfort { get; set; }

    /// <summary>
    /// امتیاز خدمات (۰-۵)
    /// </summary>
    public decimal Service { get; set; }

    /// <summary>
    /// امتیاز قیمت (۰-۵)
    /// </summary>
    public decimal Value { get; set; }

    /// <summary>
    /// امتیاز مکان (۰-۵)
    /// </summary>
    public decimal Location { get; set; }
}

/// <summary>
/// امتیازات تفصیلی یک نقد
/// </summary>
public sealed class DetailedRatingsDto
{
    /// <summary>
    /// نظافت
    /// </summary>
    public decimal? Cleanliness { get; set; }

    /// <summary>
    /// کمفورت
    /// </summary>
    public decimal? Comfort { get; set; }

    /// <summary>
    /// خدمات
    /// </summary>
    public decimal? Service { get; set; }

    /// <summary>
    /// ارزش پول
    /// </summary>
    public decimal? ValueForMoney { get; set; }

    /// <summary>
    /// موقعیت
    /// </summary>
    public decimal? Location { get; set; }

    /// <summary>
    /// WiFi
    /// </summary>
    public decimal? WiFi { get; set; }

    /// <summary>
    /// صبحانه
    /// </summary>
    public decimal? Breakfast { get; set; }
}
