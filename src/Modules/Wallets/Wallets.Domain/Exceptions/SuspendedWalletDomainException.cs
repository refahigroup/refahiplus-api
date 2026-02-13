using Wallets.Domain.Exceptions.Abstraction;

namespace Wallets.Domain.Exceptions;

public class SuspendedWalletDomainException: WalletDomainException
{
    public SuspendedWalletDomainException(string message): base("WALLET_SUSPENDED", message)
    {
        
    }
}
