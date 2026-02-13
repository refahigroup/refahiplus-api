using Wallets.Domain.Exceptions.Abstraction;

namespace Wallets.Domain.Exceptions;

public class CurrencyInvalidWalletDomainException: WalletDomainException
{
    public CurrencyInvalidWalletDomainException(string message): base("CURRENCY_INVALID", message)
    {
        
    }
}
