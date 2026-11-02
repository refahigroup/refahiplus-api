namespace Refahi.Modules.Hotels.Application.Contracts.Catalogue;
public sealed record HotelDiscoveryProfile(Guid HotelKey,int ReferenceCityId,string CityName,string ProvinceName,string Provider,string Title,string Description,string Address,int Stars,IReadOnlyList<string> Images,IReadOnlyList<string> Facilities,string? Policies,DateTimeOffset ObservedAtUtc,bool IsStale,int? ProvinceId = null,double? Latitude = null,double? Longitude = null, HotelStaticContent? Content = null, bool IsLocalContent = false, string? NativeId = null);
public sealed record HotelDiscoveryPage(IReadOnlyList<HotelDiscoveryProfile> Items,int? TotalCount,bool IsPartial);
public sealed record HotelDiscoverySearch(string? Query = null, int? CityId = null, int? ProvinceId = null, double? Latitude = null, double? Longitude = null);
public interface IHotelDiscoveryReader
{
 Task<HotelDiscoveryPage> SearchAsync(string? query,int? cityId,int take,CancellationToken ct);
 Task<HotelDiscoveryPage> SearchAsync(HotelDiscoverySearch query,int take,CancellationToken ct) => SearchAsync(query.Query,query.CityId,take,ct);
 Task<HotelDiscoveryProfile?> ProfileAsync(Guid key,CancellationToken ct);
 Task<IReadOnlyList<HotelCityDto>> CitiesAsync(CancellationToken ct);
}
public sealed class HotelDiscoveryOptions
{
 public bool WorkerEnabled { get; set; }
 public int RefreshHours { get; set; } = 24;
 public int MaxAgeDays { get; set; } = 7;
 public int RetryMinutes { get; set; } = 15;
 public int RequestTimeoutSeconds { get; set; } = 30;
}
