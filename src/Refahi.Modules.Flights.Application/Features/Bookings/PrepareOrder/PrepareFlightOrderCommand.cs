using MediatR;
using Refahi.Modules.Flights.Application.Features.Bookings;

namespace Refahi.Modules.Flights.Application.Features.Bookings.PrepareOrder;

public sealed record PrepareFlightOrderCommand(
    Guid BookingId,
    Guid UserId,
    string CallerRole,
    string IdempotencyKey) : IRequest<PrepareFlightOrderResponse>;
