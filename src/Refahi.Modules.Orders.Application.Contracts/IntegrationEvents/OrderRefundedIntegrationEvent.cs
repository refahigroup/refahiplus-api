using MediatR;

namespace Refahi.Modules.Orders.Application.Contracts.IntegrationEvents;

public sealed record OrderRefundedIntegrationEvent(
    Guid OrderId, string OrderNumber, Guid? SourceOwnerId, Guid? SourceShopId,
    long AmountMinor, DateTimeOffset OccurredAt) : INotification;
