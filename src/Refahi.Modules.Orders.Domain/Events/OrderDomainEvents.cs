using Refahi.Shared.Domain;

namespace Refahi.Modules.Orders.Domain.Events;

public sealed record OrderCreatedEvent(
    Guid OrderId,
    string OrderNumber,
    Guid UserId,
    string SourceModule,
    Guid SourceReferenceId,
    string ReferenceType,
    Guid? SagaId,
    long FinalAmountMinor,
    DateTimeOffset OccurredAt
) : IDomainEvent;

public sealed record OrderPaidEvent(
    Guid OrderId,
    string OrderNumber,
    Guid UserId,
    string SourceModule,
    Guid SourceReferenceId,
    string ReferenceType,
    Guid? SagaId,
    Guid PaymentId,
    long AmountMinor,
    DateTimeOffset OccurredAt
) : IDomainEvent;

public sealed record OrderCancelledEvent(
    Guid OrderId,
    string OrderNumber,
    Guid UserId,
    string PaymentAction,
    DateTimeOffset OccurredAt
) : IDomainEvent;

public sealed record OrderStatusChangedEvent(
    Guid OrderId,
    string OrderNumber,
    string OldStatus,
    string NewStatus,
    DateTimeOffset OccurredAt
) : IDomainEvent;

public sealed record OrderDeliveredEvent(
    Guid OrderId,
    string OrderNumber,
    Guid UserId,
    string SourceModule,
    Guid SourceReferenceId,
    string ReferenceType,
    Guid? SagaId,
    DateTimeOffset OccurredAt
) : IDomainEvent;
