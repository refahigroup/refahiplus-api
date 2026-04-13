namespace Refahi.Modules.Orders.Domain.Entities;

/// <summary>
/// OrderItem — آیتم سفارش
/// حاوی Snapshot اطلاعات در زمان خرید (مثل حسابداری)
/// </summary>
public sealed class OrderItem
{
    private OrderItem() { }

    public Guid Id { get; private set; }
    public Guid OrderId { get; private set; }

    // --- Snapshot (تغییرناپذیر بعد از ثبت) ---
    public string Title { get; private set; } = string.Empty;          // "پیراهن مردانه مشکی سایز مدیوم"
    public long UnitPriceMinor { get; private set; }                   // قیمت واحد در زمان خرید
    public int Quantity { get; private set; }
    public long DiscountAmountMinor { get; private set; }              // تخفیف این آیتم
    public long FinalPriceMinor { get; private set; }                  // (UnitPrice * Quantity) - Discount

    // --- ماژول مبدا ---
    public string SourceModule { get; private set; } = string.Empty;   // "Store"
    public Guid SourceItemId { get; private set; }                     // رفرنس به Product/Room/...

    // --- دسته‌بندی و برچسب (برای گزارشات و Wallet restriction) ---
    public string CategoryCode { get; private set; } = string.Empty;  // "store.clothing"
    public string[]? Tags { get; private set; }                        // ["پوشاک", "مردانه", "هودی"]

    // --- متادیتا (جزئیات خاص هر ماژول) ---
    public string? MetadataJson { get; private set; }                  // {"size":"XL","color":"ملانژ","variant_id":"..."}

    public int SortOrder { get; private set; }

    internal static OrderItem Create(
        Guid orderId,
        string title,
        long unitPriceMinor,
        int quantity,
        long discountAmountMinor,
        string sourceModule,
        Guid sourceItemId,
        string categoryCode,
        string[]? tags,
        string? metadataJson,
        int sortOrder)
    {
        return new OrderItem
        {
            Id = Guid.NewGuid(),
            OrderId = orderId,
            Title = title,
            UnitPriceMinor = unitPriceMinor,
            Quantity = quantity,
            DiscountAmountMinor = discountAmountMinor,
            FinalPriceMinor = (unitPriceMinor * quantity) - discountAmountMinor,
            SourceModule = sourceModule,
            SourceItemId = sourceItemId,
            CategoryCode = categoryCode,
            Tags = tags,
            MetadataJson = metadataJson,
            SortOrder = sortOrder
        };
    }
}
