namespace Refahi.Modules.SupplyChain.Domain.Enums;

public enum DeliveryType : short
{
    Shipping = 1,   // ارسال با پیک/پست (مسئولیت تامین‌کننده)
    Download = 2,   // دانلود فایل/دریافت کد
    InPerson = 3    // مراجعه حضوری به فروشگاه
}
