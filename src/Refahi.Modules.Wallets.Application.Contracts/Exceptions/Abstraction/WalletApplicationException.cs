using System;

namespace Refahi.Modules.Wallets.Application.Contracts.Exceptions.Abstraction;

public abstract class WalletApplicationException : Exception
{
    public string Code { get; }

    protected WalletApplicationException(string code, string message) : base(message)
    {
        Code = code;
    }
}
