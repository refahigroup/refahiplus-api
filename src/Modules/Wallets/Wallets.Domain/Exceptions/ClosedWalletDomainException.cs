using System;
using System.Collections.Generic;
using System.Text;
using Wallets.Domain.Exceptions.Abstraction;

namespace Wallets.Domain.Exceptions;

public class ClosedWalletDomainException: WalletDomainException
{
    public ClosedWalletDomainException(string message): base("WALLET_CLOSED", message)
    {
        
    }
}
