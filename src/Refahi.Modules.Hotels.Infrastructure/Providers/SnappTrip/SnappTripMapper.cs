using Refahi.Modules.Hotels.Application.Contracts.Providers.DTOs;
using Refahi.Modules.Hotels.Application.Contracts.Providers.DTOs.Booking;
using Refahi.Modules.Hotels.Infrastructure.Providers.SnappTrip.Contract;

namespace Refahi.Modules.Hotels.Infrastructure.Providers.SnappTrip;

public static class SnappTripMapper
{
    /// <summary>
    /// مپ کردن خروجی /availability/hotels به DTO جستجو بر اساس هتل.
    /// توجه: API جدید SnappTrip در پاسخ /availability/hotels فقط یک hotel_id و
    /// لیستی از availability برای room ها می‌دهد و اطلاعات نام هتل، شهر و تصویر را
    /// باید از /hotels بگیریم. بنابراین این متد فعلاً فقط فیلدهای قابل‌استخراج را پر می‌کند.
    /// </summary>
    public static IEnumerable<HotelSearchByHotelResultDto> MapSearchResults(SnappTripAvailabilityResponse dto)
    {
        if (dto.availability == null || dto.availability.Count == 0)
        {
            return Enumerable.Empty<HotelSearchByHotelResultDto>();
        }

        var minPrice = dto.availability
            .Select(a => (decimal)a.pricing.price)
            .DefaultIfEmpty(0m)
            .Min();

        var firstAvailability = dto.availability.FirstOrDefault();
        var accommodationType = firstAvailability?.room.accommodation_type ?? string.Empty;

        // در این مرحله name/cityName/thumbnail در این پاسخ وجود ندارد،
        // اگر نیاز داشتیم می‌توانیم قبل از این مپر، پاسخ /hotels را هم join کنیم.
        var result = new HotelSearchByHotelResultDto
        {
            HotelId = dto.hotel_id,
            Name = string.Empty,
            CityName = string.Empty,
            Stars = 0,
            AccommodationType = accommodationType,
            MinCustomerPrice = minPrice,
            ThumbnailUrl = null
        };

        return new[] { result };
    }

    /// <summary>
    /// این متد مبتنی بر مدل قدیمی SnappTrip نوشته شده بود که
    /// hotel + facilities + rooms را در یک DTO واحد برمی‌گرداند.
    /// در API جدید، ما این تجمیع را در SnappTripProvider انجام می‌دهیم
    /// (با چندین call: /hotels, /hotels/rooms, /hotels/facilities, /hotels/galleries, /availability/hotels)
    /// و مستقیماً HotelDetailsDto را در همان Provider می‌سازیم.
    /// بنابراین این متد فعلاً استفاده نمی‌شود و عمداً NotImplemented است تا اشتباهاً استفاده نشود.
    /// </summary>
    public static HotelDetailsDto MapHotelDetails(SnappTripHotelDetailsResponse h)
    {
        throw new NotImplementedException(
            "MapHotelDetails در SnappTripMapper دیگر استفاده نمی‌شود. " +
            "جزئیات هتل در SnappTripProvider.GetHotelDetailsAsync ساخته می‌شود.");
    }

    /// <summary>
    /// مپ کردن نتیجه /booking/create به نتیجهٔ استاندارد Provider.
    /// در API جدید SnappTrip، فیلدها مطابق reservation_code, price, state هستند
    /// و مفهوم lock_seconds در پاسخ وجود ندارد (Lock از طریق /booking/{code}/lock انجام می‌شود).
    /// </summary>
    public static BookingCreateResultDto MapCreateBooking(SnappTripBookingCreateResponse r)
    {
        return new BookingCreateResultDto
        {
            BookingCode = r.reservation_code,
            Price = r.price,
            Currency = "IRR",       // SnappTrip ریال برمی‌گرداند؛ اگر بعداً لازم شد می‌توانیم تنظیم‌پذیرش کنیم
            LockedUntil = null      // Lock جداگانه با /booking/{code}/lock هندل می‌شود
        };
    }

    /// <summary>
    /// مپ کردن وضعیت بوکینگ. در Swagger فعلی فقط reservation_code, state, price, rooms و ...
    /// تعریف شده است و فیلدهای voucher_url / voucher_number در مدل استاندارد نیستند.
    /// اگر SnappTrip در محیط واقعی این فیلدها را اضافه کند، باید DTO SnappTripBookingStatusResponse
    /// را هم‌راستا کنیم و اینجا مقدار دهی کنیم.
    /// </summary>
    public static BookingStatusDto MapStatus(SnappTripBookingStatusResponse r)
    {
        return new BookingStatusDto
        {
            Status = r.state,
            VoucherUrl = null,      // در مدل فعلی موجود نیست
            VoucherNumber = null,   // در مدل فعلی موجود نیست
            Message = null  // می‌توانیم در صورت نیاز از فیلدهای اضافی SnappTrip پر کنیم
        };
    }
}
