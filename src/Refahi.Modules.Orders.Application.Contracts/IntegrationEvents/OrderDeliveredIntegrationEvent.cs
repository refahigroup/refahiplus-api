using Refahi.Shared.Domain;

namespace Refahi.Modules.Orders.Application.Contracts.IntegrationEvents;

/// <summary>
/// منتشر می‌شود وقتی یک سفارش به وضعیت Delivered رسید
/// SourceModule == "Store" → Store module را فعال می‌کند تا شمارنده تحویل فروشگاه را بروز کند
/// </summary>
public sealed record OrderDeliveredIntegrationEvent(
    Guid EventId,
    Guid OrderId,
    string OrderNumber,
    Guid UserId,
    string SourceModule,
    Guid SourceReferenceId,
    DateTimeOffset OccurredAt
) : IIntegrationEvent;
