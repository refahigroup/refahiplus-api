namespace Refahi.Modules.Orders.Domain.Enums;

/// <summary>
/// وضعیت پرداخت
/// </summary>
public enum PaymentState : short
{
    Unpaid = 1,         // پرداخت نشده
    Reserved = 2,       // مبلغ رزرو شده (PaymentIntent created)
    Paid = 3,           // پرداخت شده (PaymentIntent captured)
    Refunded = 4,       // بازگشت داده شده
    Released = 5        // آزاد شده (لغو قبل از Capture)
}
