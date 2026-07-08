using Refahi.Modules.Wallets.Domain.Enums;
using Refahi.Modules.Wallets.Infrastructure.Persistence.Atomic;
using Xunit;

namespace Refahi.Modules.Wallets.Tests;

public sealed class LedgerEntryInsertCommandTests
{
    private static readonly string[] RequiredColumns =
    [
        "ledger_entry_id",
        "wallet_id",
        "operation_id",
        "operation_type",
        "entry_type",
        "amount_minor",
        "currency",
        "effective_at",
        "created_at",
        "related_entry_id",
        "relation_type",
        "external_reference",
        "metadata"
    ];

    [Fact]
    public void CommandText_IncludesEveryRequiredLedgerColumnAndParameter()
    {
        var sql = LedgerEntryInsertCommand.CommandText.ToLowerInvariant();

        foreach (var column in RequiredColumns)
            Assert.Contains(column, sql);

        Assert.Contains("@RelatedEntryId", LedgerEntryInsertCommand.CommandText);
        Assert.Contains("@RelationType", LedgerEntryInsertCommand.CommandText);
    }

    [Theory]
    [InlineData(OperationType.Reserve, EntryType.Hold)]
    [InlineData(OperationType.Payment, EntryType.Debit)]
    [InlineData(OperationType.Release, EntryType.ReleaseHold)]
    [InlineData(OperationType.Refund, EntryType.Credit)]
    public void CreateParameters_AlwaysCarriesExplicitRelationType(
        OperationType operationType,
        EntryType entryType)
    {
        var parameters = LedgerEntryInsertCommand.CreateParameters(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            operationType,
            entryType,
            100,
            "IRR",
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow,
            relatedEntryId: null,
            RelationType.None,
            externalReference: null,
            metadataJson: null);

        Assert.Equal((short)RelationType.None, parameters.RelationType);
        Assert.Null(parameters.RelatedEntryId);
        Assert.Equal((short)operationType, parameters.OperationType);
        Assert.Equal((short)entryType, parameters.EntryType);
    }
}
