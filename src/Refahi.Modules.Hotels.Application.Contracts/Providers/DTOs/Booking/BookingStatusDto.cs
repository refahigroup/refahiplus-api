namespace Refahi.Modules.Hotels.Application.Contracts.Providers.DTOs.Booking;

/// <summary>
/// وضعیت رزرو
/// این DTO برای Application layer است و provider-agnostic است
/// </summary>
public sealed class BookingStatusDto
{
    /// <summary>
    /// وضعیت رزرو (Pending, Confirmed, Cancelled, etc.)
    /// </summary>
    public string Status { get; set; } = default!;

    /// <summary>
    /// لینک به voucher (اگر موجود باشد)
    /// </summary>
    public string? VoucherUrl { get; set; }

    /// <summary>
    /// شماره voucher (شماره تائید)
    /// </summary>
    public string? VoucherNumber { get; set; }

    /// <summary>
    /// پیام یا توضیح اضافی
    /// </summary>
    public string? Message { get; set; }

    /// <summary>
    /// Alias for Message (backwards compatibility)
    /// </summary>
    public string? ProviderMessage => Message;
}
