using System;
using Refahi.Modules.Wallets.Application.Contracts.Exceptions.Abstraction;

namespace Refahi.Modules.Wallets.Application.Contracts.Exceptions;

public sealed class WalletNotFoundException : WalletApplicationException
{
    public WalletNotFoundException(Guid walletId)
        : base("WALLET_NOT_FOUND", $"Wallet {walletId} was not found.") { }
}
