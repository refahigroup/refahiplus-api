namespace Refahi.Modules.Store.Domain.Enums;

public enum ShopStatus : short
{
    PendingApproval = 1,  // در انتظار تایید ادمین
    Active = 2,           // فعال
    Suspended = 3,        // تعلیق شده
    Closed = 4            // بسته شده
}
