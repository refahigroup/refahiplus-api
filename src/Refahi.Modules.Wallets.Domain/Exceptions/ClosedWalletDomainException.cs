using System;
using System.Collections.Generic;
using System.Text;
using Refahi.Modules.Wallets.Domain.Exceptions.Abstraction;

namespace Refahi.Modules.Wallets.Domain.Exceptions;

public class ClosedWalletDomainException : WalletDomainException
{
    public ClosedWalletDomainException(string message)
        : base("WALLET_CLOSED", message) { }
}
