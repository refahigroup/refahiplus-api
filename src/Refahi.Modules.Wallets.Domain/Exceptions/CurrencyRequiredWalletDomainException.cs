using Refahi.Modules.Wallets.Domain.Exceptions.Abstraction;

namespace Refahi.Modules.Wallets.Domain.Exceptions;

public class CurrencyRequiredWalletDomainException: WalletDomainException
{
    public CurrencyRequiredWalletDomainException(string message): base("CURRENCY_REQUIRED", message)
    {
        
    }
}
