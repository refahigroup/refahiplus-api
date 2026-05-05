namespace Refahi.Modules.Store.Application.Contracts.Dtos.Cart;

public sealed record CartDto(
    Guid CartId,
    List<CartItemDto> Items,
    long TotalMinor,                  // مجموع final price آیتم‌ها (UnitPrice × Qty)
    long OriginalTotalMinor,          // مجموع original price آیتم‌ها (OriginalUnitPrice × Qty)
    long DiscountTotalMinor,          // مجموع تخفیف کل (Original - Final)
    int TotalItems);

public sealed record CartItemDto(
    Guid Id,
    Guid ShopId,
    string? ShopName,                 // نام فروشگاه (snapshot لحظه‌ی بارگذاری)
    Guid ProductId,
    string ProductTitle,
    string? ProductImageUrl,
    Guid? VariantId,
    string? VariantLabel,
    Guid? SessionId,
    string? SessionLabel,
    int Quantity,
    long UnitPriceMinor,              // قیمت نهایی واحد (پس از تخفیف)
    long OriginalUnitPriceMinor,      // قیمت اصلی واحد (پیش از تخفیف) — اگر تخفیف ندارد = UnitPriceMinor
    int DiscountPercent,              // درصد تخفیف (0 اگر بدون تخفیف)
    long TotalPriceMinor,             // UnitPriceMinor × Quantity
    bool IsAvailable);
