using System;

namespace Refahi.Modules.Wallets.Infrastructure.Persistence.Models;

public sealed class LedgerEntryRecord
{
    public Guid LedgerEntryId { get; set; }
    public Guid WalletId { get; set; }
    public Guid OperationId { get; set; }
    public short OperationType { get; set; }
    public short EntryType { get; set; }
    public long AmountMinor { get; set; }
    public string Currency { get; set; } = null!;
    public DateTimeOffset EffectiveAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public Guid? RelatedEntryId { get; set; }
    public short RelationType { get; set; }
    public string? ExternalReference { get; set; }
    public string? MetadataJson { get; set; }
}
