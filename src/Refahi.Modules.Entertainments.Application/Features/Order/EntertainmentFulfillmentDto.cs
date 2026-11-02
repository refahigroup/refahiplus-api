namespace Refahi.Modules.Entertainments.Application.Features.Order;
public sealed record EntertainmentFulfillmentDto(Guid Id, string Status, string DeliveryStatus, IReadOnlyList<EntertainmentTicketDto> Tickets);
