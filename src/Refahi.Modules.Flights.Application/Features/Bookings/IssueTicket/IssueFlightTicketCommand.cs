using MediatR;
using Refahi.Modules.Flights.Application.Features.Bookings;

namespace Refahi.Modules.Flights.Application.Features.Bookings.IssueTicket;

public sealed record IssueFlightTicketCommand(
    Guid BookingId,
    Guid UserId,
    string CallerRole,
    string IdempotencyKey) : IRequest<IssueFlightTicketResponse>;
