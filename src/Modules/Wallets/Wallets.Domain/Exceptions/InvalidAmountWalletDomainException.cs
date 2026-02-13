using System;
using System.Collections.Generic;
using System.Text;
using Wallets.Domain.Exceptions.Abstraction;

namespace Wallets.Domain.Exceptions;

public class InvalidAmountWalletDomainException: WalletDomainException
{
    public InvalidAmountWalletDomainException(string message): base("AMOUNT_INVALID", message)
    {
        
    }
}
