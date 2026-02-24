using Refahi.Modules.Wallets.Domain.Exceptions.Abstraction;

namespace Refahi.Modules.Wallets.Domain.Exceptions;

public class SuspendedWalletDomainException: WalletDomainException
{
    public SuspendedWalletDomainException(string message): base("WALLET_SUSPENDED", message)
    {
        
    }
}
