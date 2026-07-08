#nullable enable

using Refahi.Modules.Wallets.Domain.Enums;
using System;

namespace Refahi.Modules.Wallets.Infrastructure.Persistence.Atomic;

/// <summary>
/// Defines the complete persistence contract for append-only wallet ledger entries.
/// Keeping one insert statement prevents payment flows from drifting from the schema.
/// </summary>
internal static class LedgerEntryInsertCommand
{
    internal const string CommandText = @"
insert into wallets.ledger_entries (
  ledger_entry_id,
  wallet_id,
  operation_id,
  operation_type,
  entry_type,
  amount_minor,
  currency,
  effective_at,
  created_at,
  related_entry_id,
  relation_type,
  external_reference,
  metadata
)
values (
  @LedgerEntryId,
  @WalletId,
  @OperationId,
  @OperationType,
  @EntryType,
  @AmountMinor,
  @Currency,
  @EffectiveAt,
  @CreatedAt,
  @RelatedEntryId,
  @RelationType,
  @ExternalReference,
  case when @MetadataJson is null then null else cast(@MetadataJson as jsonb) end
);";

    internal static LedgerEntryInsertParameters CreateParameters(
        Guid ledgerEntryId,
        Guid walletId,
        Guid operationId,
        OperationType operationType,
        EntryType entryType,
        long amountMinor,
        string currency,
        DateTimeOffset effectiveAt,
        DateTimeOffset createdAt,
        Guid? relatedEntryId,
        RelationType relationType,
        string? externalReference,
        string? metadataJson) =>
        new(
            ledgerEntryId,
            walletId,
            operationId,
            (short)operationType,
            (short)entryType,
            amountMinor,
            currency,
            effectiveAt,
            createdAt,
            relatedEntryId,
            (short)relationType,
            externalReference,
            metadataJson);
}

internal sealed record LedgerEntryInsertParameters(
    Guid LedgerEntryId,
    Guid WalletId,
    Guid OperationId,
    short OperationType,
    short EntryType,
    long AmountMinor,
    string Currency,
    DateTimeOffset EffectiveAt,
    DateTimeOffset CreatedAt,
    Guid? RelatedEntryId,
    short RelationType,
    string? ExternalReference,
    string? MetadataJson);
