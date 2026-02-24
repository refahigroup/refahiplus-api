using System;

namespace Refahi.Modules.Wallets.Infrastructure.Persistence.Models;

/// <summary>
/// Projection table (NOT source of truth).
/// Derived/materialized balance from append-only ledger.
/// </summary>
public sealed class WalletBalanceRecord
{
    public Guid WalletId { get; set; }
    public long AvailableMinor { get; set; }
    public long PendingMinor { get; set; }
    public string Currency { get; set; } = null!;
    public Guid? LastLedgerEntryId { get; set; }
    public long Version { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
