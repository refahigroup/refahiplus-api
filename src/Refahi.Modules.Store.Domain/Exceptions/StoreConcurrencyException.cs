namespace Refahi.Modules.Store.Domain.Exceptions;

public class StoreConcurrencyException : Exception
{
    public StoreConcurrencyException()
        : base("تعارض همزمانی در ذخیره‌سازی رخ داده است")
    {
    }
}
