using System.Text;
namespace Refahi.Modules.Catalogue.Application.Contracts;
public sealed record CatalogueQuery(string? Q = null, int? CityId = null, string[]? Sources = null, string? Sort = null, int Page = 1, int PageSize = 20, string? Location = null, int? ProvinceId = null, double? Latitude = null, double? Longitude = null)
{
 public string Mode => Location ?? (CityId.HasValue ? "city" : ProvinceId.HasValue ? "province" : "all");
 public string EffectiveSort => Sort ?? (Mode == "nearby" ? "distance-asc" : "title-asc");
 public CataloguePoint? Origin => Mode == "nearby" && Latitude.HasValue && Longitude.HasValue ? new(Latitude.Value, Longitude.Value) : null;
}
public sealed record CataloguePrice(long AmountMinor, string Currency, string Unit);
public sealed record CatalogueItem(string Id, string Source, string Kind, string SourceItemId, string Title, string? Summary, string? ImageUrl, int? CityId, string? CityName, string? ProvinceName, IReadOnlyList<string> Labels, string DetailPath, DateTimeOffset ObservedAtUtc, bool IsStale, CataloguePrice? Price = null, CatalogueCategory? Category = null, IReadOnlyList<CatalogueAttribute>? Attributes = null, IReadOnlyList<CatalogueLocation>? Locations = null, CatalogueLocation? DisplayLocation = null, double? DistanceMeters = null);
public sealed record CatalogueCategory(string Code, string Title);
public sealed record CatalogueAttribute(string Key, string Title, string Value);
public sealed record CataloguePoint(double Latitude, double Longitude)
{
 public bool IsValid => double.IsFinite(Latitude) && double.IsFinite(Longitude) && Latitude is >= -90 and <= 90 && Longitude is >= -180 and <= 180;
}
public sealed record CatalogueLocation(string Id, int? ProvinceId, string? ProvinceName, int? CityId, string? CityName, string? Name = null, string? Address = null, CataloguePoint? Point = null);
public sealed record CatalogueCity(int Id, string Name, string ProvinceName, int ProvinceId = 0);
public sealed record CatalogueProvince(int Id, string Name);
public sealed record CatalogueLocationsResponse(IReadOnlyList<CatalogueProvince> Provinces, IReadOnlyList<CatalogueCity> Cities, bool IsPartial, IReadOnlyList<CatalogueSourceStatus> Sources);
public sealed record CatalogueSourceResult(IReadOnlyList<CatalogueItem> Items, int? TotalCount, bool IsPartial);
public sealed record CatalogueSourceStatus(string Source, string Status);
public sealed record CatalogueResponse(IReadOnlyList<CatalogueItem> Items, int Page, int PageSize, int? TotalCount, bool HasMore, bool IsPartial, IReadOnlyList<CatalogueSourceStatus> Sources);
public sealed record CatalogueSourceRegistration(string Key, Type Implementation);
public interface ICatalogueSource
{
 Task<CatalogueSourceResult> SearchAsync(CatalogueQuery query, int take, CancellationToken ct);
 Task<IReadOnlyList<CatalogueCity>> CitiesAsync(CancellationToken ct);
}
public sealed class CatalogueUnavailableException() : Exception("کاتالوگ در دسترس نیست؛ دوباره تلاش کنید.");
public sealed class CatalogueOptions
{
 public bool Enabled { get; set; }
 public int SourceTimeoutSeconds { get; set; } = 3;
 public int TimeoutSeconds { get; set; } = 5;
 public int MaxConcurrency { get; set; } = 4;
}
public interface ICatalogueExecutor
{
 IReadOnlyList<string> Keys { get; }
 Task<T> ExecuteAsync<T>(string key, Func<ICatalogueSource,CancellationToken,Task<T>> action, CancellationToken ct);
}
public static class CatalogueText
{
 public static string Normalize(string? value) => string.Join(" ", (value ?? "").Normalize(NormalizationForm.FormKC).ToLowerInvariant().Replace('ي','ی').Replace('ك','ک').Replace('\u200c',' ').Select(c => c is >= '۰' and <= '۹' ? (char)('0'+c-'۰') : c is >= '٠' and <= '٩' ? (char)('0'+c-'٠') : c).ToArray().AsSpan().ToString().Split((char[]?)null,StringSplitOptions.RemoveEmptyEntries));
 public static IComparer<string> Comparer { get; } = System.Collections.Generic.Comparer<string>.Create((a,b) => Encoding.UTF8.GetBytes(a).AsSpan().SequenceCompareTo(Encoding.UTF8.GetBytes(b)));
}

public sealed record SearchCatalogueQuery(CatalogueQuery Query):MediatR.IRequest<CatalogueResponse>;
public sealed record GetCatalogueCitiesQuery:MediatR.IRequest<IReadOnlyList<CatalogueCity>>;
public sealed record GetCatalogueLocationsQuery:MediatR.IRequest<CatalogueLocationsResponse>;
