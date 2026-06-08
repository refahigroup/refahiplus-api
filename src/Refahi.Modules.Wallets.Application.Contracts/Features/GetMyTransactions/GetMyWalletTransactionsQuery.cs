using MediatR;
using System;
using System.Collections.Generic;

namespace Refahi.Modules.Wallets.Application.Contracts.Features.GetMyTransactions;

public sealed record GetMyWalletTransactionsQuery(
    Guid UserId,
    int Take = 20,
    string? WalletType = null,
    short? OperationType = null,
    short? EntryType = null) : IRequest<IReadOnlyList<MyWalletTransactionDto>>;

public sealed record MyWalletTransactionDto(
    Guid LedgerEntryId,
    Guid WalletId,
    string WalletType,
    Guid OperationId,
    short OperationType,
    short EntryType,
    long AmountMinor,
    string Currency,
    DateTimeOffset EffectiveAt,
    DateTimeOffset CreatedAt,
    Guid? RelatedEntryId,
    short RelationType,
    string? ExternalReference);
