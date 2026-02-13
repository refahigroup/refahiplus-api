namespace Wallets.Domain.Enums;

/// <summary>
/// Storage contract: persisted as SMALLINT. Values must never be renumbered.
/// </summary>
public enum WalletStatus : short
{
    Active = 1,
    Suspended = 2,
    Closed = 3
}
