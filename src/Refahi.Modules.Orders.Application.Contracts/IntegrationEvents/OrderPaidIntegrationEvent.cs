using MediatR;

namespace Refahi.Modules.Orders.Application.Contracts.IntegrationEvents;

public sealed record OrderPaidIntegrationEvent(
    Guid OrderId,
    string OrderNumber,
    Guid UserId,
    string SourceModule,
    Guid SourceReferenceId,
    string ReferenceType,
    Guid? SagaId,
    Guid PaymentId,
    long AmountMinor,
    DateTimeOffset OccurredAt) : INotification;
