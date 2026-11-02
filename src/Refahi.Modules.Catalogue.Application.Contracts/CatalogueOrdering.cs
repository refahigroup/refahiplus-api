namespace Refahi.Modules.Catalogue.Application.Contracts;

/// <summary>Shared source-prefix and final merge ordering. Distances are never rounded for ranking.</summary>
public static class CatalogueOrdering
{
    public static double? Distance(CataloguePoint? origin, CataloguePoint? point)
    {
        if (origin is not { IsValid: true } || point is not { IsValid: true }) return null;
        const double radians = Math.PI / 180;
        var lat = (point.Latitude - origin.Latitude) * radians;
        var lon = (point.Longitude - origin.Longitude) * radians;
        var a = Math.Pow(Math.Sin(lat / 2), 2) + Math.Cos(origin.Latitude * radians) * Math.Cos(point.Latitude * radians) * Math.Pow(Math.Sin(lon / 2), 2);
        return 6371000 * 2 * Math.Asin(Math.Sqrt(Math.Clamp(a, 0, 1)));
    }

    public static bool Matches(CatalogueLocation location, CatalogueQuery query) => query.Mode switch
    {
        "province" => location.ProvinceId == query.ProvinceId,
        "city" => location.CityId == query.CityId && (!query.ProvinceId.HasValue || location.ProvinceId == query.ProvinceId),
        _ => true
    };

    public static CatalogueItem Prepare(CatalogueItem item, CatalogueQuery query)
    {
        // Legacy sources remain readable while they adopt Locations.
        var locations = item.Locations ?? (item.CityId.HasValue ? [new CatalogueLocation(item.SourceItemId, null, item.ProvinceName, item.CityId, item.CityName)] : Array.Empty<CatalogueLocation>());
        var display = locations.Where(x => Matches(x, query))
            .OrderBy(x => Distance(query.Origin, x.Point) is null)
            .ThenBy(x => Distance(query.Origin, x.Point))
            .ThenBy(x => x.Id, StringComparer.Ordinal).FirstOrDefault();
        return item with { Locations = locations, DisplayLocation = display, DistanceMeters = Distance(query.Origin, display?.Point), CityId = display?.CityId, CityName = display?.CityName, ProvinceName = display?.ProvinceName };
    }

    public static int Compare(double? leftDistance, string leftTitle, string leftSource, string leftId,
        double? rightDistance, string rightTitle, string rightSource, string rightId)
    {
        var result = (leftDistance is null).CompareTo(rightDistance is null);
        if (result == 0) result = Nullable.Compare(leftDistance, rightDistance);
        if (result == 0) result = CatalogueText.Comparer.Compare(leftTitle, rightTitle);
        if (result == 0) result = StringComparer.Ordinal.Compare(leftSource, rightSource);
        return result == 0 ? StringComparer.Ordinal.Compare(leftId, rightId) : result;
    }

    public static IComparer<CatalogueItem> Comparer { get; } = System.Collections.Generic.Comparer<CatalogueItem>.Create((a, b) =>
        Compare(a.DistanceMeters, CatalogueText.Normalize(a.Title), a.Source, a.SourceItemId,
            b.DistanceMeters, CatalogueText.Normalize(b.Title), b.Source, b.SourceItemId));
}
