using Refahi.Modules.Wallets.Domain.Common;
using Refahi.Modules.Wallets.Domain.Enums;
using Refahi.Modules.Wallets.Domain.Events;
using Refahi.Modules.Wallets.Domain.Exceptions;
using Refahi.Modules.Wallets.Domain.ValueObjects;
using System;
using System.Linq;

namespace Refahi.Modules.Wallets.Domain.Aggregates;

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
    // Parameterless ctor for EF Core
    private Wallet() { }

    public Guid Id { get; private set; }
    //public WalletOwnerType OwnerType { get; }
    public Guid OwnerId { get; private set; }
    public WalletType WalletType { get; private set; }
    public WalletStatus Status { get; private set; }
    public Currency Currency { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    // ORG_CREDIT fields (null for REFAHI wallets)
    public string? AllowedCategoryCode { get; private set; }
    public DateTimeOffset? ContractExpiresAt { get; private set; }


    public Wallet(Guid walletId, Guid ownerId, WalletType walletType, WalletStatus status, string currency,
        string? allowedCategoryCode = null, DateTimeOffset? contractExpiresAt = null)
    {
        Id = walletId;
        OwnerId = ownerId;
        WalletType = walletType;
        Status = status;
        Currency = ValueObjects.Currency.Of(currency);
        AllowedCategoryCode = allowedCategoryCode;
        ContractExpiresAt = contractExpiresAt;
    }

    /// <summary>
    /// Returns true if this wallet is allowed to pay for the given category.
    /// REFAHI wallets have no restriction. ORG_CREDIT wallets are restricted by prefix matching.
    /// </summary>
    public bool IsAllowedForCategory(string categoryCode)
    {
        if (WalletType != WalletType.OrgCredit) return true;
        if (AllowedCategoryCode is null) return true;

        // Prefix match: AllowedCategoryCode="store" allows "store.clothing"
        // Exact or sub-prefix: AllowedCategoryCode="store.clothing" only allows "store.clothing"
        return categoryCode.StartsWith(AllowedCategoryCode, StringComparison.OrdinalIgnoreCase)
            || AllowedCategoryCode.StartsWith(categoryCode, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Returns true if the contract has not expired.</summary>
    public bool IsContractValid() =>
        ContractExpiresAt is null || ContractExpiresAt > DateTimeOffset.UtcNow;


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
