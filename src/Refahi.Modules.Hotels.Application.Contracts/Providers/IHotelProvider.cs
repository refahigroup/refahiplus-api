using Refahi.Modules.Hotels.Application.Contracts.Providers.DTOs;
using Refahi.Modules.Hotels.Application.Contracts.Providers.DTOs.Account;
using Refahi.Modules.Hotels.Application.Contracts.Providers.DTOs.Availability;
using Refahi.Modules.Hotels.Application.Contracts.Providers.DTOs.Availability.AvailabilityByCity;
using Refahi.Modules.Hotels.Application.Contracts.Providers.DTOs.Booking;
using Refahi.Modules.Hotels.Application.Contracts.Providers.DTOs.Hotel;
using Refahi.Modules.Hotels.Application.Contracts.Providers.Queries;
using Refahi.Modules.Hotels.Application.Contracts.Services.Statics.Cities;

namespace Refahi.Modules.Hotels.Application.Contracts.Providers;

/// <summary>
/// Provider interface برای تمام عملیات هتل‌های
/// این interface abstraction برای کار با پروایدرهای مختلف (SnappTrip, AlibabaTravels, etc.)
/// بین Application layer و Infrastructure layer ارتباط برقرار می‌کند
/// </summary>
public interface IHotelProvider
{
    // ============================================================
    // 📍 STATIC DATA & SEARCH
    // ============================================================

    /// <summary>
    /// دریافت لیست تمام شهرها
    /// </summary>
    Task<IEnumerable<GetCitiesResponse>> GetAllCities(string? name);

    /// <summary>
    /// دریافت دسترسی هتل‌های یک شهر (نتایج جستجو)
    /// </summary>
    Task<GetAvailabilityByCityDto> GetAvailabilityByCity(GetAvailabilityByCityQuery query);

    // ============================================================
    // 🏨 HOTEL INFORMATION
    // ============================================================

    /// <summary>
    /// دریافت جزئیات کامل یک یا چند هتل
    /// شامل: اطلاعات پایه، اتاق‌ها، تسهیلات، گالری، دسترسی، و نقدها
    /// </summary>
    Task<IEnumerable<HotelDetailsDto>> GetHotelDetailsAsync(GetHotelDetailsQuery query);

    /// <summary>
    /// دریافت تقویم دسترسی یک هتل برای بازه زمانی معین
    /// </summary>
    Task<AvailabilityCalendarDto> GetHotelAvailabilityCalendarAsync(long hotelId, DateOnly from, DateOnly to);

    /// <summary>
    /// دریافت نقدهای هتل
    /// </summary>
    Task<HotelReviewsDto> GetHotelReviewsAsync(long hotelId, int page = 1, int pageSize = 10);

    // ============================================================
    // 💰 ACCOUNT INFORMATION
    // ============================================================

    /// <summary>
    /// دریافت موجودی حساب (برای مدیریت داخلی)
    /// </summary>
    Task<AccountBalanceDto> GetAccountBalanceAsync();

    // ============================================================
    // 📅 BOOKING LIFECYCLE
    // ============================================================

    /// <summary>
    /// مرحله ۱: ایجاد رزرو موقت (Provisional)
    /// این متد رزرو را در حالت موقت ایجاد می‌کند و کد رزرو موقت برمی‌گرداند
    /// قیمت و دسترسی در این مرحله تائید می‌شود
    /// </summary>
    Task<BookingCreateResultDto> CreateBookingAsync(BookingDraftDto dto);

    /// <summary>
    /// مرحله ۲: قفل کردن رزرو (Lock - 15 دقیقه‌ای)
    /// این متد قیمت و دسترسی را برای 15 دقیقه ایمن می‌کند
    /// کاربر باید در این 15 دقیقه برای تایید نهایی اقدام کند
    /// </summary>
    Task LockBookingAsync(string bookingCode);

    /// <summary>
    /// مرحله ۳: تایید نهایی رزرو (Confirm)
    /// این متد رزرو را نهایی می‌کند و کد رزرو دائمی برمی‌گرداند
    /// </summary>
    Task ConfirmBookingAsync(string bookingCode);

    /// <summary>
    /// دریافت وضعیت رزرو
    /// </summary>
    Task<BookingStatusDto> GetBookingStatusAsync(string bookingCode);
}

