using Refahi.Modules.Entertainments.Application.Contracts.Providers;
using Refahi.Modules.Entertainments.Application.Contracts.Providers.Dtos;
namespace Refahi.Modules.Entertainments.Application.Contracts.Discovery;

public sealed class EntertainmentsOptions
{
    public bool Enabled { get; set; }
    public bool CatalogueEnabled { get; set; }
    public bool DiscoveryWorkerEnabled { get; set; }
    public int MaxDiscoveryAgeDays { get; set; } = 7;
}
public sealed record EntertainmentDiscoverySearch(string? Q, int? ProvinceId, int? CityId);
public sealed record EntertainmentDiscoveryItem(Guid ProgramKey, string Title, string? Summary, string? Address,
    string? ImageUrl, int? CityId, string? CityName, int? ProvinceId, string? ProvinceName,
    string? CategoryCode, string? CategoryTitle, string SupplierTitle, DateTimeOffset ObservedAtUtc, bool IsStale);
public sealed record EntertainmentDiscoveryPage(IReadOnlyList<EntertainmentDiscoveryItem> Items, int? TotalCount, bool IsPartial);
public sealed record EntertainmentDiscoveryCity(int CityId, string CityName, int ProvinceId, string ProvinceName);
public sealed record EntertainmentLocationMappingDto(string Code, string? Name, int? CityId);
public interface IEntertainmentDiscovery
{
    Task<EntertainmentDiscoveryPage> SearchAsync(EntertainmentDiscoverySearch query, int take, CancellationToken ct);
    Task<IReadOnlyList<EntertainmentDiscoveryCity>> CitiesAsync(CancellationToken ct);
    Task<EntertainmentProgramDto?> ProfileAsync(Guid programKey, CancellationToken ct);
    Task<string?> ResolveNativeAsync(string programKey, CancellationToken ct);
    Task<Guid?> ResolveProgramKeyAsync(string nativeId, CancellationToken ct);
    Task<IReadOnlyList<EntertainmentLocationMappingDto>> MappingsAsync(CancellationToken ct);
    Task SetMappingAsync(string code, int cityId, Guid actor, CancellationToken ct);
}
public sealed class EntertainmentUnavailableException() : Exception("اطلاعات خدمات تفریحی هنوز آماده نیست؛ دوباره تلاش کنید.");
