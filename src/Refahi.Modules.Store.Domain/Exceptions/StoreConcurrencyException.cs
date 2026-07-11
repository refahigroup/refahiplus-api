namespace Refahi.Modules.Store.Domain.Exceptions;

public class StoreConcurrencyException : Exception
{
    public StoreConcurrencyException(Exception? innerException = null)
        : base("تعارض همزمانی در ذخیره‌سازی رخ داده است", innerException)
    {
    }
}
