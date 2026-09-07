using FluentValidation;
using MediatR;
using Refahi.Modules.Flights.Domain.Repositories;

namespace Refahi.Modules.Flights.Application.Features.Locations;

public sealed record GetFlightLocationsQuery(bool IsDomestic, string? Query, int Limit = 20)
    : IRequest<GetFlightLocationsResponse>;
public sealed record FlightLocationDto(string Code, string Type, string CityCode, string CityNameFa,
    string CityNameEn, string CountryCode, string CountryNameFa, string CountryNameEn,
    int AirportCount, string? AirportNameFa, string? AirportNameEn);
public sealed record GetFlightLocationsResponse(IReadOnlyCollection<FlightLocationDto> Locations);

public sealed class GetFlightLocationsValidator : AbstractValidator<GetFlightLocationsQuery>
{
    public GetFlightLocationsValidator()
    {
        RuleFor(x => x.Query).MaximumLength(200).WithMessage("عبارت جست‌وجو حداکثر ۲۰۰ نویسه باشد.");
        RuleFor(x => x.Limit).InclusiveBetween(1, 50).WithMessage("تعداد نتایج باید بین ۱ و ۵۰ باشد.");
    }
}
public sealed class GetFlightLocationsHandler(IFlightLocationRepository repository)
    : IRequestHandler<GetFlightLocationsQuery, GetFlightLocationsResponse>
{
    public async Task<GetFlightLocationsResponse> Handle(GetFlightLocationsQuery request, CancellationToken ct)
    {
        var rows = await repository.SearchAsync(request.IsDomestic, request.Query, request.Limit, ct);
        return new(rows.Select(r => new FlightLocationDto(r.Code, r.Type, r.CityCode, r.CityNameFa,
            r.CityNameEn, r.CountryCode, r.CountryNameFa, r.CountryNameEn, r.AirportCount,
            r.AirportNameFa, r.AirportNameEn)).ToList());
    }
}

