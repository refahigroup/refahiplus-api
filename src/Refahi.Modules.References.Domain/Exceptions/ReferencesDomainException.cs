namespace Refahi.Modules.References.Domain.Exceptions;

public class ReferencesDomainException : Exception
{
    public string ErrorCode { get; }

    public ReferencesDomainException(string message, string errorCode = "REFERENCES_DOMAIN_ERROR")
        : base(message)
    {
        ErrorCode = errorCode;
    }
}
