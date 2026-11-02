using MediatR;
using Refahi.Modules.Flights.Domain.Repositories;
using Refahi.Modules.Flights.Application.Contracts.Providers;

namespace Refahi.Modules.Flights.Application.Features.Airports.GetFlightAirports;

public sealed class GetFlightAirportsQueryHandler
    : IRequestHandler<GetFlightAirportsQuery, GetFlightAirportsResponse>
{
    private readonly IFlightAirportRepository _airportRepository;
    private readonly IFlightGeographyService? _geography;

    public GetFlightAirportsQueryHandler(IFlightAirportRepository airportRepository, IFlightGeographyService? geography = null)
    {
        _airportRepository = airportRepository;
        _geography = geography;
    }

    public async Task<GetFlightAirportsResponse> Handle(
        GetFlightAirportsQuery request,
        CancellationToken cancellationToken
    )
    {
        var airports = await _airportRepository.SearchAsync(
            request.Query,
            Math.Clamp(request.Limit, 1, 50),
            cancellationToken
        );
        var refs = _geography is null ? new Dictionary<string, FlightReferenceGeography>()
            : await _geography.ReadAsync(false, airports.Select(a => a.IataCode).ToArray(), cancellationToken);
        return new GetFlightAirportsResponse(
            airports
                .Select(airport => new FlightAirportDto(
                    airport.IataCode,
                    airport.CityCode,
                    refs.GetValueOrDefault(airport.IataCode)?.CityNameFa ?? airport.CityNameFa,
                    refs.GetValueOrDefault(airport.IataCode)?.CityNameEn ?? airport.CityNameEn,
                    airport.AirportNameFa,
                    airport.AirportNameEn,
                    refs.GetValueOrDefault(airport.IataCode)?.CountryCode ?? airport.CountryCode,
                    airport.IsPopular,
                    refs.GetValueOrDefault(airport.IataCode)?.CountryNameFa ?? airport.CountryNameFa,
                    refs.GetValueOrDefault(airport.IataCode)?.CountryNameEn ?? airport.CountryNameEn,
                    airport.IcaoCode,
                    refs.GetValueOrDefault(airport.IataCode)?.CityId,
                    refs.GetValueOrDefault(airport.IataCode)?.ProvinceId,
                    refs.GetValueOrDefault(airport.IataCode)?.CountryCode
                ))
                .ToList()
        );
    }
}
