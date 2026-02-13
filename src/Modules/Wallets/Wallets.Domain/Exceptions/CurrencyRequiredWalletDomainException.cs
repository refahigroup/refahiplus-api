using Wallets.Domain.Exceptions.Abstraction;

namespace Wallets.Domain.Exceptions;

public class CurrencyRequiredWalletDomainException: WalletDomainException
{
    public CurrencyRequiredWalletDomainException(string message): base("CURRENCY_REQUIRED", message)
    {
        
    }
}
