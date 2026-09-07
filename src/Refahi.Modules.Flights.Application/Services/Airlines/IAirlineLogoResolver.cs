namespace Refahi.Modules.Flights.Application.Services.Airlines;

public sealed record AirlinePresentation(string? Name, string? LogoUrl);

public interface IAirlineLogoResolver
{
    Task<string?> ResolveAsync(string? airlineCode, CancellationToken cancellationToken);

    Task<IReadOnlyDictionary<string, string?>> ResolveAsync(
        IEnumerable<string?> airlineCodes,
        CancellationToken cancellationToken
    );

    Task<IReadOnlyDictionary<string, AirlinePresentation>> ResolvePresentationsAsync(
        IEnumerable<string?> airlineCodes,
        CancellationToken cancellationToken
    );
}
