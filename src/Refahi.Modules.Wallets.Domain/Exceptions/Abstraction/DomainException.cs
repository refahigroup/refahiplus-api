using System;

namespace Refahi.Modules.Wallets.Domain.Exceptions.Abstraction;

public abstract class DomainException: Exception
{
    public string Code { get; protected set; }

    protected DomainException(string code, string message): base(message)
    {
        Code = code;
    }
}
