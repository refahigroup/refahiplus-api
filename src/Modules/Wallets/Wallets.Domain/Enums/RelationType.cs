namespace Wallets.Domain.Enums;

/// <summary>
/// Storage contract: persisted as SMALLINT. Values must never be renumbered.
/// </summary>
public enum RelationType : short
{
    None = 0,
    Reversal = 1,
    Refund = 2,
    Adjustment = 3
}
