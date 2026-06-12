using MediatR;
using Refahi.Modules.Flights.Application.Features.Bookings;

namespace Refahi.Modules.Flights.Application.Features.Bookings.GetBookingDetail;

public sealed record GetFlightBookingDetailQuery(
    Guid BookingId,
    Guid UserId,
    string CallerRole) : IRequest<FlightBookingDetailDto?>;
