namespace Refahi.Modules.Store.Application.Services;

/// <summary>
/// پیاده‌سازی Hardcoded هزینه ارسال — فاز ۱.
/// قیمت‌ها به ریال (Minor unit) — تومان × ۱۰.
/// </summary>
public sealed class DeliveryService : IDeliveryService
{
    // قیمت‌های پایه (هر کدام به ریال)
    private const long ProviderDeliveryFlatFee = 0L;             // ارسال فروشنده — رایگان (پیش‌فرض)
    private const long RefahiDeliveryFlatFee = 100_000L;         // ارسال رفاهی — ۱۰٬۰۰۰ تومان (۱۰۰٬۰۰۰ ریال)
    private const long InStorePickupFee = 0L;                    // تحویل حضوری — رایگان

    public long CalcPrice(IReadOnlyList<DeliveryItemInput> items, Guid? shippingAddressId = null, Guid? shopId = null)
    {
        if (items is null || items.Count == 0)
            return 0L;

        // قانون فاز ۱: حداکثرِ هزینه‌ی روش‌های انتخاب‌شده برای کل سبد (نه per-item).
        // یعنی اگر کاربر یک آیتم را «ارسال رفاهی» انتخاب کند و بقیه را «ارسال فروشنده»، هزینه = هزینه‌ی رفاهی.
        long max = 0;
        foreach (var item in items)
        {
            var fee = GetMethodFee(item.DeliveryMethod);
            if (fee > max) max = fee;
        }
        return max;
    }

    private static long GetMethodFee(short method) => method switch
    {
        1 => ProviderDeliveryFlatFee,   // ProviderDelivery
        2 => RefahiDeliveryFlatFee,     // RefahiDelivery
        3 => InStorePickupFee,          // InStorePickup
        _ => 0L                         // None / unknown
    };
}
