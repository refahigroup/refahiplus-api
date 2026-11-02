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
}
public interface IEntertainmentContentReader
{
    Task<EntertainmentProgramDto?> ProfileAsync(Guid programKey, CancellationToken ct);
    Task<EntertainmentCatalogPage> ProgramsAsync(string? search, string? supplierKey, int page, int size, string? location, string? category, CancellationToken ct);
    Task<EntertainmentSupplierPage> SuppliersAsync(int page, int size, string? location, string? category, CancellationToken ct);
    Task<EntertainmentSupplierDto?> SupplierAsync(Guid supplierKey, CancellationToken ct);
    Task<EntertainmentCatalogFilters> FiltersAsync(CancellationToken ct);
}
public sealed record EntertainmentProgramIdentity(string ProviderKey, string NativeId, string SupplierId);
public interface IEntertainmentProgramIdentity
{
    Task<EntertainmentProgramIdentity?> ResolveAsync(Guid programKey, CancellationToken ct);
    Task<string?> ResolveNativeAsync(string programKey, CancellationToken ct);
}
public interface IEntertainmentLocationMappings
{
    Task<IReadOnlyList<EntertainmentLocationMappingDto>> MappingsAsync(CancellationToken ct);
    Task SetMappingAsync(string code, int cityId, Guid actor, CancellationToken ct);
}
public sealed class EntertainmentUnavailableException() : Exception("اطلاعات خدمات تفریحی هنوز آماده نیست؛ دوباره تلاش کنید.");
