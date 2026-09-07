using Refahi.Modules.Flights.Application.Services.Airlines;

namespace Refahi.Modules.Flights.Tests;

internal sealed class StubAirlineLogoResolver(string? logoUrl = null) : IAirlineLogoResolver
{
    public Task<string?> ResolveAsync(string? airlineCode, CancellationToken cancellationToken) =>
        Task.FromResult(logoUrl);

    public Task<IReadOnlyDictionary<string, string?>> ResolveAsync(
        IEnumerable<string?> airlineCodes,
        CancellationToken cancellationToken
    ) => Task.FromResult<IReadOnlyDictionary<string, string?>>(
        airlineCodes
            .Where(code => !string.IsNullOrWhiteSpace(code))
            .Select(code => code!.Trim().ToUpperInvariant())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToDictionary(code => code, _ => logoUrl, StringComparer.OrdinalIgnoreCase)
    );

    public Task<IReadOnlyDictionary<string, AirlinePresentation>> ResolvePresentationsAsync(
        IEnumerable<string?> airlineCodes,
        CancellationToken cancellationToken
    ) => Task.FromResult<IReadOnlyDictionary<string, AirlinePresentation>>(
        airlineCodes
            .Where(code => !string.IsNullOrWhiteSpace(code))
            .Select(code => code!.Trim().ToUpperInvariant())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                code => code,
                code => new AirlinePresentation(code == "IR" ? "ایران ایر" : null, logoUrl),
                StringComparer.OrdinalIgnoreCase
            )
    );
}
