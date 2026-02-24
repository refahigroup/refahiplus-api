using Refahi.Modules.Wallets.Domain.Exceptions.Abstraction;

namespace Refahi.Modules.Wallets.Domain.Exceptions;

public class CurrencyInvalidWalletDomainException: WalletDomainException
{
    public CurrencyInvalidWalletDomainException(string message): base("CURRENCY_INVALID", message)
    {
        
    }
}
