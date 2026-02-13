namespace Wallets.Domain.Enums;

/// <summary>
/// Storage contract: persisted as SMALLINT. Values must never be renumbered.
/// </summary>
public enum EntryType : short
{
    Credit = 1,
    Debit = 2,
    Hold = 3,
    ReleaseHold = 4
}
