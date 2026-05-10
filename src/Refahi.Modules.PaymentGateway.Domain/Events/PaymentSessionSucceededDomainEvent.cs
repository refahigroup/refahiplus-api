using Refahi.Modules.PaymentGateway.Domain.Enums;
using Refahi.Shared.Domain;
using System;

namespace Refahi.Modules.PaymentGateway.Domain.Events;

public sealed record PaymentSessionSucceededDomainEvent(
    Guid SessionId,
    Guid UserId,
    Guid WalletId,
    long AmountMinor,
    string Currency,
    PaymentGatewayProviderType Provider,
    Guid TopUpLedgerEntryId,
    DateTimeOffset OccurredAt
) : IDomainEvent;
