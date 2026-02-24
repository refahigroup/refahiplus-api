namespace Refahi.Modules.Hotels.Application.Contracts.Providers.DTOs.Booking;

/// <summary>
/// نتیجه‌ی ایجاد رزرو
/// این DTO برای Application layer است و provider-agnostic است
/// هر provider می‌تواند نتایج خودش رو به این DTO map کند
/// </summary>
public sealed class BookingCreateResultDto
{
    /// <summary>
    /// کد رزرو (یکتا برای هر رزرو در سیستم)
    /// </summary>
    public string BookingCode { get; set; } = default!;

    /// <summary>
    /// قیمت نهایی (تومان)
    /// </summary>
    public long Price { get; set; }

    /// <summary>
    /// واحد پول (معمولاً IRR)
    /// </summary>
    public string Currency { get; set; } = "IRR";

    /// <summary>
    /// زمان انقضای قفل (اگر قبلاً قفل شده باشد)
    /// </summary>
    public DateTime? LockedUntil { get; set; }

    /// <summary>
    /// Alias for BookingCode (backwards compatibility)
    /// </summary>
    public string ProviderBookingCode => BookingCode;

    /// <summary>
    /// Alias for Price (backwards compatibility)
    /// </summary>
    public long ProviderPrice => Price;
}
