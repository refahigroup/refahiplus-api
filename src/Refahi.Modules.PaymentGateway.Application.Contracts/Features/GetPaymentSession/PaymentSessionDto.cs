using Refahi.Modules.PaymentGateway.Domain.Enums;
using System;

namespace Refahi.Modules.PaymentGateway.Application.Contracts.Features.GetPaymentSession;

public sealed record PaymentSessionDto(
    Guid SessionId,
    PaymentSessionStatus Status,
    long AmountMinor,
    string Currency,
    PaymentGatewayProviderType Provider,
    DateTimeOffset InitiatedAt,
    DateTimeOffset? CompletedAt,
    Guid? TopUpLedgerEntryId,
    int? ProviderResultCode,
    string? ProviderResultDescription
);
