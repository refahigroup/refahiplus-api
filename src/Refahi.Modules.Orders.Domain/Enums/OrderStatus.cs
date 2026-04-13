namespace Refahi.Modules.Orders.Domain.Enums;

/// <summary>
/// وضعیت سفارش — State Machine
/// Pending → Confirmed → Processing → Shipped → Delivered
///                                  └→ Cancelled
///        └→ Cancelled (قبل از تایید)
/// </summary>
public enum OrderStatus : short
{
    Pending = 1,        // ثبت شده، در انتظار پرداخت
    Confirmed = 2,      // پرداخت شده، تایید شده
    Processing = 3,     // در حال پردازش توسط تامین‌کننده
    Shipped = 4,        // ارسال شده
    Delivered = 5,      // تحویل داده شده
    Cancelled = 6,      // لغو شده
    Refunded = 7        // بازگشت وجه شده
}
