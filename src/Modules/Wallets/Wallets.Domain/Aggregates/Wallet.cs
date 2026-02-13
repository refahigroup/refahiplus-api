using System;
using System.Linq;
using Wallets.Domain.Common;
using Wallets.Domain.Enums;
using Wallets.Domain.Events;
using Wallets.Domain.Exceptions;
using Wallets.Domain.ValueObjects;

namespace Wallets.Domain.Aggregates;

/// <summary>
/// Aggregate Root: Wallet
///
/// Domain-only invariants:
/// - Single currency per wallet (immutable).
/// - Ledger is append-only (enforced at DB; domain assumes entries are immutable).
/// - Wallet status gates operations (e.g., Closed cannot accept money ops).
///
/// Persistence concerns (EF/Dapper) are intentionally excluded.
/// </summary>
public sealed class Wallet : EntityBase
{
    public Guid Id { get; }
    public WalletOwnerType OwnerType { get; }
    public Guid OwnerId { get; }
    public WalletType WalletType { get; }
    public WalletStatus Status { get; private set; }
    public Currency Currency { get; }
    public DateTimeOffset CreatedAt { get; private set; }


    public Wallet(Guid walletId, WalletOwnerType ownerType, Guid ownerId, WalletType walletType, WalletStatus status, string currency)
    {
        Id = walletId;
        OwnerType = ownerType;
        OwnerId = ownerId;
        WalletType = walletType;
        Status = status;

        Currency = ValueObjects.Currency.Of(currency);
    }


    /// <summary>
    /// Domain method: Create a TopUp ledger entry.
    /// Validates invariants and constructs immutable LedgerEntry.
    /// </summary>
    public LedgerEntry CreateTopUpEntry(
        Guid ledgerEntryId,
        Guid operationId,
        long amountMinor,
        string? externalReference = null,
        string? metadataJson = null)
    {
        EnsureCanAcceptCredit();

        var money = Money.Of(amountMinor, Currency);
        var entry = new LedgerEntry(
            ledgerEntryId: ledgerEntryId,
            walletId: Id,
            operationId: operationId,
            operationType: OperationType.TopUp,
            entryType: EntryType.Credit,
            money: money,
            externalReference: externalReference,
            metadataJson: metadataJson);

        // Publish domain event
        AddDomainEvent(new WalletToppedUpDomainEvent(
            WalletId: Id,
            OperationId: operationId,
            LedgerEntryId: entry.Id,
            Amount: money,
            OccurredAt: DateTimeOffset.UtcNow,
            ExternalReference: externalReference,
            MetadataJson: metadataJson));

        return entry;
    }

    public void EnsureCanAcceptCredit()
    {
        if (Status is WalletStatus.Closed)
            throw new ClosedWalletDomainException("Wallet is closed.");

        if (Status is WalletStatus.Suspended)
            throw new SuspendedWalletDomainException("Wallet is suspended.");
    }

    /// <summary>
    /// Static helper: Normalize currency (delegated to Currency VO).
    /// Kept for backward compatibility.
    /// </summary>
    public static string NormalizeCurrency(string currency)
    {
        return ValueObjects.Currency.Of(currency).Code;
    }
}
