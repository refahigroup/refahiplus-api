using MediatR;

namespace Refahi.Modules.Hotels.Application.Contracts.Services.Bookings.PrepareOrder;

public sealed record PrepareHotelOrderCommand(
    Guid BookingId,
    Guid UserId,
    string IdempotencyKey
) : IRequest<PrepareHotelOrderResponse>;

public sealed record PrepareHotelOrderResponse(
    Guid BookingId,
    Guid OrderId,
    string OrderNumber,
    long FinalAmountMinor,
    string Status);
