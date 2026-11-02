namespace Refahi.Modules.Entertainments.Domain;

public sealed class EntertainmentDomainException(string message, string errorCode) : Exception(message)
{
    public string ErrorCode { get; } = errorCode;
}
