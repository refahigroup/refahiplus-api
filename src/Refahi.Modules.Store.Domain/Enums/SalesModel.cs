namespace Refahi.Modules.Store.Domain.Enums;

/// <summary>
/// مدل فروش محصول
/// </summary>
public enum SalesModel : short
{
    Unlimited = 1,
    InventoryBased = 2,
    SessionBased = 3,

    [Obsolete("نام legacy است؛ از InventoryBased استفاده کنید.")]
    StockBased = InventoryBased
}
