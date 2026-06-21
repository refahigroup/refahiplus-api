namespace Refahi.Modules.Store.Domain.Enums;

/// <summary>
/// نوع ظرفیت قابل اعمال روی تنوع محصول
/// </summary>
public enum VariantCapacityType : short
{
    /// <summary>
    /// بدون محدودیت ظرفیت فروش
    /// </summary>
    Unlimited = 0,

    /// <summary>
    /// ظرفیت کل برای تمام بازه اعتبار
    /// </summary>
    TotalPeriod = 1,

    /// <summary>
    /// ظرفیت جداگانه برای هر روز مجاز
    /// </summary>
    PerEligibleDay = 2
}
