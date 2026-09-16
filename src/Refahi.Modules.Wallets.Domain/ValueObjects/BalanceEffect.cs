namespace Refahi.Modules.Wallets.Domain.ValueObjects;

/// <summary>
/// The explicit, immutable balance effect of a ledger entry.
/// Ledger projections must sum these deltas and must not infer accounting semantics
/// from an operation name or entry direction.
/// </summary>
public readonly record struct BalanceEffect(long AvailableDeltaMinor, long PendingDeltaMinor)
{
    public static BalanceEffect TopUp(long amountMinor) => new(amountMinor, 0);
    public static BalanceEffect Reserve(long amountMinor) => new(-amountMinor, amountMinor);
    public static BalanceEffect Capture(long amountMinor) => new(0, -amountMinor);
    public static BalanceEffect Release(long amountMinor) => new(amountMinor, -amountMinor);
    public static BalanceEffect AvailableCredit(long amountMinor) => new(amountMinor, 0);
    public static BalanceEffect AvailableDebit(long amountMinor) => new(-amountMinor, 0);
}
