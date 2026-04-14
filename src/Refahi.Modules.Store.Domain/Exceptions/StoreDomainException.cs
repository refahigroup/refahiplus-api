namespace Refahi.Modules.Store.Domain.Exceptions;

public class StoreDomainException : Exception
{
    public string ErrorCode { get; }

    public StoreDomainException(string message, string errorCode = "STORE_DOMAIN_ERROR")
        : base(message)
    {
        ErrorCode = errorCode;
    }
}
