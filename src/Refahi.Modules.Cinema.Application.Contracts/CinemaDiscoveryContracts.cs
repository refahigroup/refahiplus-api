namespace Refahi.Modules.Cinema.Application.Contracts;

public sealed class CinemaDiscoveryOptions
{
    public bool WorkerEnabled { get; set; }
    public bool CatalogueEnabled { get; set; }
    public int RefreshMinutes { get; set; } = 5;
    public int StaleMinutes { get; set; } = 15;
    public int MaxAgeMinutes { get; set; } = 60;
    public int HorizonDays { get; set; } = 14;
}

public sealed record CinemaScheduleReference(string ProviderKey, string ExternalId);

public interface ICinemaDiscoveryReader
{
    Task<IReadOnlyList<CinemaCity>> CitiesAsync(CancellationToken ct);
    Task<CinemaShow> ShowAsync(Guid show, CancellationToken ct);
    Task<IReadOnlyList<CinemaDisplayDay>> PlacesAsync(Guid show, int? city, string? search, CancellationToken ct);
    Task<IReadOnlyList<CinemaSession>> SessionsAsync(Guid show, Guid place, string? date, CancellationToken ct);
    Task<CinemaScheduleReference?> FindScheduleAsync(Guid schedule, CancellationToken ct);
}

public static class CinemaScheduleIdentity
{
    public static bool IsLegacyId(string? value) => value is { Length: 26 } && value.All(char.IsAsciiLetterOrDigit);
    public static bool IsValid(string? value) => IsLegacyId(value) || Guid.TryParseExact(value, "D", out _);

    public static async Task<CinemaScheduleReference> ResolveAsync(string id, ICinemaDiscoveryReader? discovery, CancellationToken ct)
    {
        if (IsLegacyId(id)) return new("iticket", id);
        if (Guid.TryParseExact(id, "D", out var key) && discovery is not null
            && await discovery.FindScheduleAsync(key, ct) is { } reference)
            return reference;
        throw new CinemaException("سانس موردنظر یافت نشد", 404);
    }
}
