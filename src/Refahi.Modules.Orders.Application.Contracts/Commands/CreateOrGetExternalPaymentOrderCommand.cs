using MediatR;

namespace Refahi.Modules.Orders.Application.Contracts.Commands;

/// <summary>
/// Creates or resolves one product-neutral payable Order for a trusted external
/// payment source. The source reference and idempotency key must be stable.
/// </summary>
public sealed record CreateOrGetExternalPaymentOrderCommand(
    Guid UserId,
    string SourceModule,
    Guid SourceReferenceId,
    Guid? SourceOwnerId,
    long AmountMinor,
    string Currency,
    string CategoryCode,
    string Title,
    string IdempotencyKey,
    DateTimeOffset PayableUntil
) : IRequest<ExternalPaymentOrderDto>;

public sealed record ExternalPaymentOrderDto(
    Guid OrderId,
    Guid UserId,
    string SourceModule,
    Guid SourceReferenceId,
    string ReferenceType,
    Guid? SourceOwnerId,
    long AmountMinor,
    string Currency,
    string CategoryCode,
    string PaymentState,
    DateTimeOffset? PayableUntil,
    bool WasCreated
);
