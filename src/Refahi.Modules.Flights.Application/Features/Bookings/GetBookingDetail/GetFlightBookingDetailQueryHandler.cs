using MediatR;
using Refahi.Modules.Flights.Application.Features.Bookings;
using Refahi.Modules.Flights.Domain.Aggregates.FlightBookingAgg.ValueObjects;
using Refahi.Modules.Flights.Domain.Repositories;

namespace Refahi.Modules.Flights.Application.Features.Bookings.GetBookingDetail;

public sealed class GetFlightBookingDetailQueryHandler
    : IRequestHandler<GetFlightBookingDetailQuery, FlightBookingDetailDto?>
{
    private readonly IFlightBookingRepository _bookingRepository;

    public GetFlightBookingDetailQueryHandler(IFlightBookingRepository bookingRepository)
    {
        _bookingRepository = bookingRepository;
    }

    public async Task<FlightBookingDetailDto?> Handle(
        GetFlightBookingDetailQuery request,
        CancellationToken cancellationToken)
    {
        var booking = await _bookingRepository.GetAsync(new FlightBookingId(request.BookingId), cancellationToken);
        if (booking is null)
            return null;

        if (!string.Equals(request.CallerRole, "Admin", StringComparison.OrdinalIgnoreCase)
            && booking.UserId != request.UserId)
        {
            return null;
        }

        return FlightBookingDtoMapper.ToDetailDto(booking);
    }
}
