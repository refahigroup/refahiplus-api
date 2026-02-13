using Wallets.Application.Contracts.Exceptions.Abstraction;

namespace Wallets.Application.Contracts.Exceptions;

public sealed class WalletCurrencyMismatchException : WalletApplicationException
{
    public WalletCurrencyMismatchException(string expected, string provided)
        : base("CURRENCY_MISMATCH", $"Wallet currency is {expected} but request currency is {provided}.") { }
}
