namespace Refahi.Modules.Store.Domain.Enums;

public enum ProductType : short
{
    Goods = 1,
    Service = 2,

    [Obsolete("نام legacy است؛ از Goods استفاده کنید.")]
    Physical = Goods,
    [Obsolete("نوع Digital در مدل جدید با FulfillmentMethod مشخص می‌شود.")]
    Digital = Service
}
