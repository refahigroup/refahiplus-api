using System;
using Wallets.Domain.Enums;
using Wallets.Domain.Exceptions;
using Wallets.Domain.ValueObjects;

namespace Wallets.Domain.Aggregates;

/// <summary>
/// Domain entity representing a single immutable posting.
///
/// IMPORTANT:
/// - Amount is always positive. Direction is determined by EntryType.
/// - This entity is not responsible for computing balances.
/// </summary>
public sealed class LedgerEntry
{
    public Guid Id { get; }
    public Guid WalletId { get; }
    public Guid OperationId { get; }
    public OperationType OperationType { get; }
    public EntryType EntryType { get; }
    public Money Money { get; }
    
    /// <summary>
    /// Amount in minor units (compatibility property).
    /// </summary>
    public long AmountMinor => Money.AmountMinor;
    
    /// <summary>
    /// Currency code (compatibility property).
    /// </summary>
    public string Currency => Money.Currency.Code;
    
    public DateTimeOffset EffectiveAt { get; }
    public DateTimeOffset CreatedAt { get; }

    public Guid? RelatedEntryId { get; }
    public RelationType RelationType { get; }

    public string? ExternalReference { get; }
    public string? MetadataJson { get; }

    /// <summary>
    /// Primary constructor: takes Money value object.
    /// </summary>
    public LedgerEntry(
        Guid ledgerEntryId,
        Guid walletId,
        Guid operationId,
        OperationType operationType,
        EntryType entryType,
        Money money,
        Guid? relatedEntryId = null,
        RelationType relationType = RelationType.None,
        string? externalReference = null,
        string? metadataJson = null)
    {
        Id = ledgerEntryId;
        WalletId = walletId;
        OperationId = operationId;
        OperationType = operationType;
        EntryType = entryType;
        Money = money ?? throw new ArgumentNullException(nameof(money));
        EffectiveAt = DateTimeOffset.UtcNow;
        CreatedAt = DateTimeOffset.UtcNow;

        RelatedEntryId = relatedEntryId;
        RelationType = relationType;

        ExternalReference = externalReference;
        MetadataJson = metadataJson;
    }

    /// <summary>
    /// Compatibility constructor: takes primitive amount/currency.
    /// Deprecated: Use Money-based constructor instead.
    /// </summary>
    public LedgerEntry(
        Guid ledgerEntryId,
        Guid walletId,
        Guid operationId,
        OperationType operationType,
        EntryType entryType,
        long amountMinor,
        string currency,
        DateTimeOffset effectiveAt,
        DateTimeOffset createdAt,
        Guid? relatedEntryId = null,
        RelationType relationType = RelationType.None,
        string? externalReference = null,
        string? metadataJson = null)
    {
        if (amountMinor <= 0)
            throw new InvalidAmountWalletDomainException("Amount must be positive (minor units).");

        Id = ledgerEntryId;
        WalletId = walletId;
        OperationId = operationId;
        OperationType = operationType;
        EntryType = entryType;
        Money = ValueObjects.Money.Of(amountMinor, Wallet.NormalizeCurrency(currency));
        EffectiveAt = effectiveAt;
        CreatedAt = createdAt;

        RelatedEntryId = relatedEntryId;
        RelationType = relationType;

        ExternalReference = externalReference;
        MetadataJson = metadataJson;
    }
}
