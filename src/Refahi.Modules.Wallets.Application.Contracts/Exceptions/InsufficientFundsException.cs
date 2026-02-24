using Refahi.Modules.Wallets.Application.Contracts.Exceptions.Abstraction;
using System;
using System.Net.Sockets;

namespace Refahi.Modules.Wallets.Application.Contracts.Exceptions;

public sealed class InsufficientFundsException : WalletApplicationException
{
    public InsufficientFundsException(Guid walletId, long availableMinor, long amountMinor)
        : base("InsufficientFunds WALLET_INSUFFICIENT_FUNDS", $"Operation is not allowed, Insufficient funds.") { }
}
