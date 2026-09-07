using System.Reflection;
using MediatR;
using Microsoft.Extensions.Configuration;
using Refahi.Modules.Media.Application.Contracts.Queries;

namespace Refahi.Modules.Flights.Application.Services.Airlines;

public sealed class AirlineLogoResolver : IAirlineLogoResolver
{
    private const string CatalogResourceName =
        "Refahi.Modules.Flights.Application.Data.iata_airlines.csv";
    private const string MediaRoot = "airline-logos";

    private static readonly Lazy<AirlineCatalog> Catalog =
        new(LoadCatalog, LazyThreadSafetyMode.ExecutionAndPublication);

    private readonly ISender _sender;
    private readonly IReadOnlyDictionary<string, string> _overrides;
    private readonly IReadOnlyDictionary<string, string> _nameOverrides;

    public AirlineLogoResolver(ISender sender, IConfiguration configuration)
    {
        _sender = sender;
        _overrides = configuration
            .GetSection("Flights:AirlineLogos:CodeOverrides")
            .GetChildren()
            .Select(item => new
            {
                Iata = Normalize(item.Key),
                Icao = Normalize(item.Value),
            })
            .Where(item => item.Iata?.Length == 2 && item.Icao?.Length == 3)
            .ToDictionary(item => item.Iata!, item => item.Icao!, StringComparer.OrdinalIgnoreCase);
        _nameOverrides = configuration
            .GetSection("Flights:AirlineLogos:NameOverrides")
            .GetChildren()
            .Select(item => new { Code = Normalize(item.Key), Name = item.Value?.Trim() })
            .Where(item => item.Code is not null && !string.IsNullOrWhiteSpace(item.Name))
            .ToDictionary(item => item.Code!, item => item.Name!, StringComparer.OrdinalIgnoreCase);
    }

    public async Task<string?> ResolveAsync(
        string? airlineCode,
        CancellationToken cancellationToken
    )
    {
        var normalized = Normalize(airlineCode);
        if (normalized is null)
            return null;

        var resolved = await ResolvePresentationsAsync([normalized], cancellationToken);
        return resolved.GetValueOrDefault(normalized)?.LogoUrl;
    }

    public async Task<IReadOnlyDictionary<string, string?>> ResolveAsync(
        IEnumerable<string?> airlineCodes,
        CancellationToken cancellationToken
    )
    {
        var presentations = await ResolvePresentationsAsync(airlineCodes, cancellationToken);
        return presentations.ToDictionary(
            item => item.Key,
            item => item.Value.LogoUrl,
            StringComparer.OrdinalIgnoreCase
        );
    }

    public async Task<IReadOnlyDictionary<string, AirlinePresentation>> ResolvePresentationsAsync(
        IEnumerable<string?> airlineCodes,
        CancellationToken cancellationToken
    )
    {
        var normalizedCodes = airlineCodes
            .Select(Normalize)
            .Where(code => code is not null)
            .Select(code => code!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var codeToIcao = normalizedCodes
            .Select(code => new { Code = code, Icao = ToIcao(code) })
            .Where(item => item.Icao is not null)
            .ToDictionary(item => item.Code, item => item.Icao!, StringComparer.OrdinalIgnoreCase);

        var candidateSets = codeToIcao.Values
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(icao => new MediaPublicUrlCandidateSet(
                icao,
                [
                    $"{MediaRoot}/flightaware_logos/{icao}.png",
                    $"{MediaRoot}/radarbox_logos/{icao}.png",
                ]
            ))
            .ToList();

        if (candidateSets.Count == 0)
            return normalizedCodes.ToDictionary(
                code => code,
                code => new AirlinePresentation(ResolveName(code, codeToIcao), null),
                StringComparer.OrdinalIgnoreCase
            );

        var mediaUrls = await _sender.Send(
            new ResolveMediaPublicUrlsQuery(candidateSets),
            cancellationToken
        );
        var byIcao = mediaUrls.ToDictionary(
            item => item.Key,
            item => item.PublicUrl,
            StringComparer.OrdinalIgnoreCase
        );

        return normalizedCodes.ToDictionary(
            code => code,
            code => new AirlinePresentation(
                ResolveName(code, codeToIcao),
                codeToIcao.TryGetValue(code, out var icao)
                    ? byIcao.GetValueOrDefault(icao)
                    : null
            ),
            StringComparer.OrdinalIgnoreCase
        );
    }

    private string? ResolveName(
        string code,
        IReadOnlyDictionary<string, string> codeToIcao
    )
    {
        if (_nameOverrides.TryGetValue(code, out var overriddenName))
            return overriddenName;

        if (Catalog.Value.NamesByCode.TryGetValue(code, out var name))
            return name;

        return codeToIcao.TryGetValue(code, out var icao)
            ? Catalog.Value.NamesByCode.GetValueOrDefault(icao)
            : null;
    }

    private string? ToIcao(string normalizedCode)
    {
        if (normalizedCode.Length == 3)
            return normalizedCode;

        if (_overrides.TryGetValue(normalizedCode, out var overridden))
            return overridden;

        return Catalog.Value.IataToIcao.GetValueOrDefault(normalizedCode);
    }

    private static string? Normalize(string? code)
    {
        if (string.IsNullOrWhiteSpace(code))
            return null;

        var normalized = code.Trim().ToUpperInvariant();
        return normalized.Length is 2 or 3
            && normalized.All(character =>
                character is >= 'A' and <= 'Z' or >= '0' and <= '9')
                ? normalized
                : null;
    }

    private static AirlineCatalog LoadCatalog()
    {
        using var stream = Assembly.GetExecutingAssembly()
            .GetManifestResourceStream(CatalogResourceName)
            ?? throw new InvalidOperationException("کاتالوگ کد ایرلاین در دسترس نیست.");
        using var reader = new StreamReader(stream);

        var mappings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var names = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        while (reader.ReadLine() is { } line)
        {
            var columns = line.Split('^');
            if (columns.Length < 2)
                continue;

            var iata = Normalize(columns[0]);
            var icao = Normalize(columns[1]);
            var name = columns.Length > 2 ? columns[2].Trim() : null;
            if (iata?.Length == 2 && icao?.Length == 3)
                mappings.TryAdd(iata, icao);
            if (!string.IsNullOrWhiteSpace(name))
            {
                if (iata is not null)
                    names.TryAdd(iata, name);
                if (icao is not null)
                    names.TryAdd(icao, name);
            }
        }

        return new AirlineCatalog(mappings, names);
    }

    private sealed record AirlineCatalog(
        IReadOnlyDictionary<string, string> IataToIcao,
        IReadOnlyDictionary<string, string> NamesByCode
    );
}
