using System.Globalization;
using System.Net;
using Ganss.Xss;
using Microsoft.Extensions.Options;
using Refahi.Modules.Pools.Application.Contracts;

namespace Refahi.Modules.Pools.Infrastructure.Providers.PoolTicket;
public sealed class PoolTicketProvider(PoolTicketClient client, IOptions<PoolTicketOptions> options) : IPoolProvider
{
    public string Key => "poolticket";
    public string Name => "پول تیکت";
    public PoolCapabilities Capabilities => new(true, options.Value.CalculateReadOnlyConfirmed, false, false, false, false, false);
    public async Task<IReadOnlyList<PoolState>> StatesAsync(bool activeOnly, CancellationToken ct)
    {
        var r = await client.PostAsync<StatesResponse>("states", new() { ["just_active_states"] = activeOnly }, ct);
        return Checked(r.States).Select(x => new PoolState(Id(x.StateId), Required(x.Name))).ToArray();
    }
    public async Task<IReadOnlyList<PoolCity>> CitiesAsync(string? stateId, bool activeOnly, CancellationToken ct)
    {
        var r = await client.PostAsync<CitiesResponse>("cities", new() { ["state_id"] = OptionalId(stateId), ["just_active_cities"] = activeOnly }, ct);
        return Checked(r.Cities).Select(x => new PoolCity(Id(x.CityId), Required(x.CityName), Id(x.StateId), Required(x.StateName))).ToArray();
    }
    public async Task<PoolCatalog> SearchAsync(PoolSearchQuery q, CancellationToken ct)
    {
        // Provider supports city/area, not a state filter. State is used only for the city picker.
        var r = await client.PostAsync<CatalogResponse>($"pools/{q.Page}/{q.Limit}", new()
        {
            ["city_id"] = OptionalId(q.CityId), ["area_id"] = OptionalId(q.AreaId), ["lat"] = q.Latitude, ["lng"] = q.Longitude,
            ["pool_name"] = string.IsNullOrWhiteSpace(q.Search) ? null : q.Search.Trim(), ["gender"] = q.Gender,
            ["sort_by"] = q.Sort, ["just_active_sell"] = q.ActiveOnly, ["pool_image_width"] = 600
        }, ct);
        var items = Checked(r.Pools).Select(x => new PoolSummary(Key, Id(x.PoolId), Required(x.Name), SafeUrl(x.DefaultImage),
            x.StateName ?? "", x.CityName ?? "", x.AreaName, x.Lat, x.Lng, x.StatusSell, x.Gender, x.Rating, x.MaxDiscount,
            Price(x.MinimumSalePrice), Price(x.MinimumMarketPrice), options.Value.PriceUnit)).ToArray();
        return new(items, q.Page, q.Limit, Checked(r.SortList ?? []).Select(x => new PoolSort(Required(x.Code), Required(x.Title))).ToArray(), r.SortedBy);
    }
    public async Task<IReadOnlyList<PoolSession>> SessionsAsync(string poolId, CancellationToken ct)
    {
        var r = await client.PostAsync<SessionsResponse>("saens/list", new() { ["pool_id"] = NumericId(poolId) }, ct);
        return Checked(r.Saens).Select(s => new PoolSession(Id(s.SaensId), Required(s.SaensName), s.Gender,
            Checked(s.Rows).Select(x => new PoolSessionRow(Id(x.SaensRowId), x.StatusSell, x.AdultSaleStatus, x.ChildSaleStatus,
                Price(x.PriceSaleAdult)!.Value, Price(x.PriceSaleChild)!.Value, Price(x.PriceMarketAdult)!.Value, Price(x.PriceMarketChild)!.Value,
                Required(x.StartDate), x.StartDay ?? "", Required(x.Time), Html(x.Description, x.DescriptionType), Html(x.TermsOfUse, x.TermsOfUseType), options.Value.PriceUnit)).ToArray())).ToArray();
    }
    public async Task<string> TermsAsync(string poolId, CancellationToken ct)
    {
        var id = NumericId(poolId);
        var r = await client.PostAsync<TermsResponse>($"pool/terms-of-use/{id}", new() { ["pool_id"] = id }, ct);
        return Html(r.TermsOfUse?.Content ?? throw Invalid(), "html");
    }
    public async Task<PoolQuote> QuoteAsync(PoolSelection s, CancellationToken ct)
    {
        if (!options.Value.CalculateReadOnlyConfirmed) throw new PoolException("رفتار محاسبه قیمت تأمین‌کننده هنوز تأیید نشده است", 503);
        var sessions = await SessionsAsync(s.PoolId, ct);
        var row = sessions.SelectMany(x => x.Rows).SingleOrDefault(x => x.Id == s.SessionRowId) ?? throw new PoolException("سانس یافت نشد", 404);
        if (!row.IsSelling || s.AdultCount > 0 && !row.AdultEnabled || s.ChildCount > 0 && !row.ChildEnabled) throw new PoolException("فروش سانس غیرفعال است");
        var r = await client.PostAsync<QuoteResponse>("reservation/calculate", new()
        { ["saens_row_id"] = NumericId(s.SessionRowId), ["count_adult"] = s.AdultCount, ["count_child"] = s.ChildCount }, ct);
        var c = r.Cart ?? throw Invalid();
        if (Id(c.SaensRowId) != s.SessionRowId || c.CountAdult != s.AdultCount || c.CountChild != s.ChildCount || c.TotalSale < 0 || c.TotalMarket < 0) throw Invalid();
        return new(s.SessionRowId, c.CountAdult, c.CountChild, c.TotalSale, c.TotalMarket,
            ToMinor(c.TotalSale, options.Value.PriceUnit), options.Value.PriceUnit, Required(c.StartDate), c.StartDay ?? "", Required(c.Time));
    }
    public static long? ToMinor(decimal amount, string? unit)
    {
        if (amount < 0) throw Invalid();
        if (unit is not ("IRR" or "TOMAN")) return null;
        try { var value = checked(amount * (unit == "TOMAN" ? 10 : 1)); if (value != decimal.Truncate(value)) throw Invalid(); return checked((long)value); }
        catch (OverflowException) { throw Invalid(); }
    }
    public static string Html(string? value, string? type)
    {
        if (type != "html") return WebUtility.HtmlEncode(value ?? "");
        var sanitizer = new HtmlSanitizer(); sanitizer.AllowedTags.Clear();
        sanitizer.AllowedTags.UnionWith(["p", "br", "strong", "b", "em", "i", "u", "ul", "ol", "li", "a", "blockquote"]);
        sanitizer.AllowedAttributes.Clear(); sanitizer.AllowedAttributes.UnionWith(["href", "title"]);
        sanitizer.AllowedSchemes.Clear(); sanitizer.AllowedSchemes.UnionWith(["http", "https"]); sanitizer.AllowDataAttributes = false;
        return sanitizer.Sanitize(value ?? "");
    }
    private static string? SafeUrl(string? s) => Uri.TryCreate(s, UriKind.Absolute, out var uri) && uri.Scheme is "https" or "http" ? uri.AbsoluteUri : null;
    private static decimal? Price(decimal? p) { if (p < 0) throw Invalid(); return p; }
    private static string Required(string? s) => string.IsNullOrWhiteSpace(s) ? throw Invalid() : s;
    private static T[] Checked<T>(T[]? values) where T : class => values != null && values.All(x => x != null) ? values : throw Invalid();
    private static string Id(long id) => id > 0 ? id.ToString(CultureInfo.InvariantCulture) : throw Invalid();
    private static long? OptionalId(string? id) => string.IsNullOrWhiteSpace(id) ? null : NumericId(id);
    public static long NumericId(string id) => long.TryParse(id, NumberStyles.None, CultureInfo.InvariantCulture, out var n) && n > 0 ? n : throw new PoolException("شناسه تأمین‌کننده معتبر نیست", 400);
    private static PoolException Invalid() => new("پاسخ تأمین‌کننده معتبر نیست", 502);
}
