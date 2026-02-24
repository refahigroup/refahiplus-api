using Refahi.Modules.Wallets.Domain.Exceptions.Abstraction;
using System;
using System.Collections.Generic;
using System.Text;

namespace Refahi.Modules.Wallets.Domain.Exceptions;

public class InvalidAmountWalletDomainException: WalletDomainException
{
    public InvalidAmountWalletDomainException(string message): base("AMOUNT_INVALID", message)
    {
        
    }
}
