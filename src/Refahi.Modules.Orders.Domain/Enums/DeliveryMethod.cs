namespace Refahi.Modules.Orders.Domain.Enums;

/// <summary>
/// روش ارسال انتخاب‌شده برای یک آیتم سفارش.
/// </summary>
public enum DeliveryMethod : short
{
    /// <summary>روش ارسال هنوز انتخاب نشده / محصولی که ارسال نمی‌خواهد (مثل سرویس).</summary>
    None = 0,

    /// <summary>ارسال توسط فروشنده (پست، پیک خود فروشگاه، ...).</summary>
    ProviderDelivery = 1,

    /// <summary>ارسال توسط رفاهی (سرویس داخلی پلتفرم).</summary>
    RefahiDelivery = 2,

    /// <summary>تحویل حضوری از فروشگاه.</summary>
    InStorePickup = 3
}

/// <summary>
/// بازه‌ی ساعتی تحویل (برای فاز ۲ — فاز ۱ خالی نگه داشته می‌شود).
/// </summary>
public enum DeliveryTimeSlot : short
{
    /// <summary>بازه‌ی ساعتی انتخاب نشده.</summary>
    None = 0,

    /// <summary>۹ تا ۱۳.</summary>
    Slot_09_13 = 1,

    /// <summary>۱۲ تا ۱۶.</summary>
    Slot_12_16 = 2,

    /// <summary>۱۶ تا ۲۰.</summary>
    Slot_16_20 = 3,

    /// <summary>۱۸ تا ۲۲.</summary>
    Slot_18_22 = 4
}
