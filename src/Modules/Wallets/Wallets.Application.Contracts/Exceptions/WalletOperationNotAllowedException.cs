using Wallets.Application.Contracts.Exceptions.Abstraction;

namespace Wallets.Application.Contracts.Exceptions;

public sealed class WalletOperationNotAllowedException : WalletApplicationException
{
    public WalletOperationNotAllowedException(string status)
        : base("WALLET_OPERATION_NOT_ALLOWED", $"Operation is not allowed when wallet status is {status}.") { }
}
