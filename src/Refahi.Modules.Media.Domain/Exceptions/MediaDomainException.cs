namespace Refahi.Modules.Media.Domain.Exceptions;

public sealed class MediaDomainException : Exception
{
    public string Code { get; }

    public MediaDomainException(string message, string code) : base(message)
    {
        Code = code;
    }
}
