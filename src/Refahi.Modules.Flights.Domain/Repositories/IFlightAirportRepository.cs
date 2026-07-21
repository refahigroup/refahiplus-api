using Refahi.Modules.Flights.Domain.Aggregates.FlightAirportAgg;

namespace Refahi.Modules.Flights.Domain.Repositories;

public interface IFlightAirportRepository
{
    Task<IReadOnlyList<FlightAirport>> SearchAsync(string? query, int limit, CancellationToken cancellationToken);
    Task<IReadOnlyList<FlightAirport>> GetByIataCodesAsync(
        IReadOnlyCollection<string> iataCodes,
        CancellationToken cancellationToken);
}
