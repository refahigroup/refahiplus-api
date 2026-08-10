using MediatR;

namespace Refahi.Modules.Orders.Application.Contracts.IntegrationEvents;

public sealed record OrderCancelledIntegrationEvent(
    Guid OrderId,
    string OrderNumber,
    Guid UserId,
    string SourceModule,
    Guid? SourceReferenceId,
    string ReferenceType,
    string PaymentAction,
    DateTimeOffset OccurredAt
) : INotification;
