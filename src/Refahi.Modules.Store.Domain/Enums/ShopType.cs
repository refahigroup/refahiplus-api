namespace Refahi.Modules.Store.Domain.Enums;

public enum ShopType : short
{
    Online = 1,
    InPerson = 2,

    [Obsolete("نام legacy است؛ از InPerson استفاده کنید.")]
    Physical = InPerson,
}
