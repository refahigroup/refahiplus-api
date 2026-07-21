using MediatR;
using Refahi.Modules.Flights.Domain.Repositories;

namespace Refahi.Modules.Flights.Application.Features.Airports.GetFlightAirports;

public sealed class GetFlightAirportsQueryHandler
    : IRequestHandler<GetFlightAirportsQuery, GetFlightAirportsResponse>
{
    private readonly IFlightAirportRepository _airportRepository;

    public GetFlightAirportsQueryHandler(IFlightAirportRepository airportRepository)
        => _airportRepository = airportRepository;

    public async Task<GetFlightAirportsResponse> Handle(GetFlightAirportsQuery request, CancellationToken cancellationToken)
    {
        var airports = await _airportRepository.SearchAsync(request.Query, Math.Clamp(request.Limit, 1, 50), cancellationToken);
        return new GetFlightAirportsResponse(airports.Select(airport => new FlightAirportDto(
            airport.IataCode, airport.CityCode, airport.CityNameFa, airport.CityNameEn,
            airport.AirportNameFa, airport.AirportNameEn, airport.CountryCode, airport.IsPopular,
            airport.CountryNameFa, airport.CountryNameEn, airport.IcaoCode)).ToList());
    }
}
