namespace Refahi.Modules.Commerce.Domain;

public sealed class CommerceDomainException(string message, string errorCode) : Exception(message)
{
    public string ErrorCode { get; } = errorCode;
}
