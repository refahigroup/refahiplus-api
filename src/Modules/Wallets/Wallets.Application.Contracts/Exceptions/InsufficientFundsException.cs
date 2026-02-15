using System;
using System.Net.Sockets;
using Wallets.Application.Contracts.Exceptions.Abstraction;

namespace Wallets.Application.Contracts.Exceptions;

public sealed class InsufficientFundsException : WalletApplicationException
{
    public InsufficientFundsException(Guid walletId, long availableMinor, long amountMinor)
        : base("InsufficientFunds WALLET_INSUFFICIENT_FUNDS", $"Operation is not allowed, Insufficient funds.") { }
}
