using System;

namespace Refahi.Modules.Wallets.Application.Contracts.Features.GetTransactions;

public record GetTransactionsResponse(
    Guid LedgerEntryId,
    Guid OperationId,
    short OperationType,
    short EntryType,
    long AmountMinor,
    string Currency,
    DateTimeOffset EffectiveAt,
    DateTimeOffset CreatedAt,
    Guid? RelatedEntryId,
    short RelationType,
    string? ExternalReference
);
