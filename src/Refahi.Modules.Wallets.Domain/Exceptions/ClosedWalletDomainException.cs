using Refahi.Modules.Wallets.Domain.Exceptions.Abstraction;
using System;
using System.Collections.Generic;
using System.Text;

namespace Refahi.Modules.Wallets.Domain.Exceptions;

public class ClosedWalletDomainException: WalletDomainException
{
    public ClosedWalletDomainException(string message): base("WALLET_CLOSED", message)
    {
        
    }
}
