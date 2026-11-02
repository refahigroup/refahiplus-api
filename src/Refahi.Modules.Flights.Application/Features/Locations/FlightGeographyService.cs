using MediatR;
using Microsoft.Extensions.Logging;
using Refahi.Modules.Flights.Application.Contracts.Providers;
using Refahi.Modules.Flights.Domain.Repositories;
using Refahi.Modules.References.Application.Contracts.Queries;

namespace Refahi.Modules.Flights.Application.Features.Locations;

public sealed class FlightGeographyService(IFlightGeographyMappingRepository mappings, IMediator mediator,
    ILogger<FlightGeographyService> logger) : IFlightGeographyService
{
    public async Task<IReadOnlyDictionary<string, FlightReferenceGeography>> ReadAsync(
        bool commercial, IReadOnlyCollection<string>? codes, CancellationToken ct)
    {
        var mapping = await mappings.ReadAsync(commercial, codes, ct);
        var cities = mapping.Count == 0 ? [] : await mediator.Send(
            new GetCityGeographiesQuery(mapping.Values.Distinct().ToArray()), ct);
        var active = cities.Where(x => x.IsActive).ToDictionary(x => x.CityId);
        var result = new Dictionary<string, FlightReferenceGeography>(StringComparer.OrdinalIgnoreCase);
        foreach (var (code, cityId) in mapping)
        {
            if (active.TryGetValue(cityId, out var c))
                result[code] = new(c.CityId, c.CityName, c.CityNameEn, c.ProvinceId,
                    c.ProvinceName, c.CountryCode, c.CountryName, c.CountryNameEn);
            else
                logger.LogWarning("Flight geography fallback Commercial={Commercial} Code={Code} CityId={CityId} Reason={Reason}",
                    commercial, code, cityId, "MissingOrInactiveReference");
        }
        if (codes is not null)
            foreach (var code in codes.Distinct(StringComparer.OrdinalIgnoreCase).Where(x => !mapping.ContainsKey(x)))
                logger.LogWarning("Flight geography fallback Commercial={Commercial} Code={Code} Reason={Reason}",
                    commercial, code, "Unmapped");
        return result;
    }
}
