using MediatR;

namespace Refahi.Modules.Hotels.Application.Contracts.Services.Bookings.GetBookingDetails;

public sealed record GetHotelBookingDetailsQuery(Guid BookingId) : IRequest<HotelBookingDetailsResponse?>;

