namespace Wallets.Domain.Enums;

/// <summary>
/// Storage contract: persisted as SMALLINT. Values must never be renumbered.
/// </summary>
public enum WalletType : short
{
    System = 1,
    User = 2,
    Provider = 3
}
