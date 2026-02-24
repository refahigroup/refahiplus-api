using Refahi.Modules.Wallets.Domain.ValueObjects;
using Refahi.Shared.Domain;
using System;

namespace Refahi.Modules.Wallets.Domain.Events;

/// <summary>
/// Domain Event: Wallet has been topped up successfully.
/// Published after ledger entry creation.
/// </summary>
public sealed record WalletToppedUpDomainEvent(
    Guid WalletId,
    Guid OperationId,
    Guid LedgerEntryId,
    Money Amount,
    DateTimeOffset OccurredAt,
    string? ExternalReference = null,
    string? MetadataJson = null
) : IDomainEvent;
