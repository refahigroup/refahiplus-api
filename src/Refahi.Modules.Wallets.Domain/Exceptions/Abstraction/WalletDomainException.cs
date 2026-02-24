namespace Refahi.Modules.Wallets.Domain.Exceptions.Abstraction;

public abstract class WalletDomainException : DomainException
{
    public WalletDomainException(string code, string message) : base(code, message)
    {
    }
}
