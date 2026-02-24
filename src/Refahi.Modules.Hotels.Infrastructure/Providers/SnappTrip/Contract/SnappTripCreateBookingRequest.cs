using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Refahi.Modules.Hotels.Infrastructure.Providers.SnappTrip.Contract;

/// <summary>
/// درخواست رزرو در SnappTrip برای /booking/create
/// مطابق CreateBookReq در Swagger.
/// </summary>
public sealed class SnappTripCreateBookingRequest
{
    /// <summary>
    /// شناسه هتل در SnappTrip.
    /// </summary>
    public int hotel_id { get; set; }

    /// <summary>
    /// تاریخ ورود به فرمت YYYY-MM-DD.
    /// </summary>
    public string checkin { get; set; } = default!;

    /// <summary>
    /// تاریخ خروج به فرمت YYYY-MM-DD.
    /// </summary>
    public string checkout { get; set; } = default!;

    /// <summary>
    /// ایمیل مسافر / رزروکننده.
    /// </summary>
    public string email { get; set; } = default!;

    /// <summary>
    /// شماره موبایل (معمولاً با +98).
    /// </summary>
    public string phone { get; set; } = default!;

    /// <summary>
    /// توضیحات رزرو (اختیاری).
    /// </summary>
    public string? note { get; set; }

    /// <summary>
    /// لیست اتاق‌های درخواستی.
    /// </summary>
    public List<SnappTripBookRoom> rooms { get; set; } = new();
}

/// <summary>
/// جزئیات هر اتاق در درخواست رزرو.
/// مطابق RoomToBook در Swagger.
/// </summary>
public sealed class SnappTripBookRoom
{
    /// <summary>
    /// شناسه Room در SnappTrip.
    /// </summary>
    public int room_id { get; set; }

    /// <summary>
    /// تعداد کودکان این اتاق.
    /// </summary>
    public int children { get; set; }

    /// <summary>
    /// تعداد نوزادان (infants).
    /// </summary>
    public int infants { get; set; }

    /// <summary>
    /// تعداد تخت اضافه.
    /// </summary>
    public int extra_beds { get; set; }

    /// <summary>
    /// لیست مسافران این اتاق.
    /// </summary>
    public List<SnappTripGuest> guests { get; set; } = new();
}

/// <summary>
/// اطلاعات هر مسافر برای /booking/create.
/// </summary>
public sealed class SnappTripGuest
{
    public string first_name { get; set; } = default!;
    public string last_name { get; set; } = default!;

    /// <summary>
    /// آیا مسافر خارجی است؟
    /// </summary>
    public bool foreigner { get; set; }
}
