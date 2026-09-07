using MediatR;
using Refahi.Modules.Flights.Application.Features.Bookings;
using Refahi.Modules.Flights.Domain.Aggregates.FlightBookingAgg.ValueObjects;
using Refahi.Modules.Flights.Domain.Repositories;
using Refahi.Modules.Flights.Application.Services.Airlines;

namespace Refahi.Modules.Flights.Application.Features.Bookings.GetBookingDetail;

public sealed class GetFlightBookingDetailQueryHandler
    : IRequestHandler<GetFlightBookingDetailQuery, FlightBookingDetailDto?>
{
    private readonly IFlightBookingRepository _bookingRepository;
    private readonly IAirlineLogoResolver _airlineLogoResolver;
    private readonly IFlightLocationRepository _locationRepository;

    public GetFlightBookingDetailQueryHandler(
        IFlightBookingRepository bookingRepository,
        IAirlineLogoResolver airlineLogoResolver,
        IFlightLocationRepository locationRepository
    )
    {
        _bookingRepository = bookingRepository;
        _airlineLogoResolver = airlineLogoResolver;
        _locationRepository = locationRepository;
    }

    public async Task<FlightBookingDetailDto?> Handle(
        GetFlightBookingDetailQuery request,
        CancellationToken cancellationToken
    )
    {
        var booking = await _bookingRepository.GetAsync(
            new FlightBookingId(request.BookingId),
            cancellationToken
        );
        if (booking is null)
            return null;

        if (
            !string.Equals(request.CallerRole, "Admin", StringComparison.OrdinalIgnoreCase)
            && booking.UserId != request.UserId
        )
        {
            return null;
        }

        var airlinePresentations = await _airlineLogoResolver.ResolvePresentationsAsync(
            booking.Segments.Select(segment => segment.AirlineCode),
            cancellationToken
        );
        var airportPresentations = await _locationRepository.GetAirportPresentationsAsync(
            booking.Segments.SelectMany(segment => new[]
            {
                segment.OriginAirportCode,
                segment.DestinationAirportCode,
            }),
            cancellationToken
        );
        return FlightBookingDtoMapper.ToDetailDto(
            booking,
            airlinePresentations,
            airportPresentations
        );
    }
}
