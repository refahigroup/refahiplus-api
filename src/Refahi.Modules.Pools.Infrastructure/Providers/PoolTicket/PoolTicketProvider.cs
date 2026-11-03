using System.Globalization;
using System.Net;
using Ganss.Xss;
using Microsoft.Extensions.Options;
using Refahi.Modules.Pools.Application.Contracts;

namespace Refahi.Modules.Pools.Infrastructure.Providers.PoolTicket;
public sealed class PoolTicketProvider(PoolTicketClient client, IOptions<PoolTicketOptions> options) : IPoolOrganizationProvider
{
    public string Key => "poolticket";
    public string Name => "پول تیکت";
    public PoolCapabilities Capabilities =>
        new(true, options.Value.CalculateReadOnlyConfirmed, true, false, true, false, false);

    private IReadOnlyList<PoolState>? states;
    public async Task<IReadOnlyList<PoolState>> StatesAsync(bool activeOnly, CancellationToken ct)
    {
        var r = await client.PostOrganizationAsync<StatesResponse>("state/list", new()
        {
            ["just_active_states"] = activeOnly
        }, ct);

        return states = Checked(r.States)
            .Select(x => new PoolState(Id(x.StateId), Required(x.StateName)))
            .ToArray();
    }

    public async Task<IReadOnlyList<PoolCity>> CitiesAsync(string? stateId, bool activeOnly, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(stateId))
        {
            var cities = new List<PoolCity>();
            foreach (var state in await StatesAsync(activeOnly, ct)) cities.AddRange(await CitiesAsync(state.Id, activeOnly, ct));
            return cities;
        }
        var stateName = (states ?? await StatesAsync(false, ct)).SingleOrDefault(x => x.Id == stateId)?.Name ?? throw Invalid();
        var r = await client.PostOrganizationAsync<CitiesResponse>("city/list", new()
        { ["state_id"] = NumericId(stateId), ["just_active_cities"] = activeOnly }, ct);
        return Checked(r.Cities).Select(x => new PoolCity(Id(x.CityId), Required(x.CityName), stateId, stateName)).ToArray();
    }

    public async Task<PoolCatalog> SearchAsync(PoolSearchQuery q, CancellationToken ct)
    {
        if (q.Latitude.HasValue && !string.IsNullOrWhiteSpace(q.CityId))
            throw new PoolException("شهر و مختصات همزمان مجاز نیستند", 400);
        var r = await client.PostOrganizationAsync<CatalogResponse>($"pool/list/{q.Page}/{q.Limit}", new()
        {
            ["city"] = OptionalId(q.CityId), ["lat"] = q.Latitude, ["lng"] = q.Longitude,
            ["search_keyword"] = string.IsNullOrWhiteSpace(q.Search) ? null : q.Search.Trim(),
            ["status_sell"] = q.ActiveOnly == true ? (object)true : null, ["image_width"] = 600
        }, ct);
        if (r.Page != q.Page || r.Limit != q.Limit || r.TotalPages is null or < 0) throw Invalid();
        var items = Checked(r.Pools).Select(x => new PoolSummary(
            Key,
            Id(x.PoolId),
            Required(x.Name),
            SafeUrl(x.DefaultImage),
            x.StateName ?? "",
            x.CityName ?? "",
            x.AreaName,
            x.Lat,
            x.Lng,
            x.StatusSell,
            x.Gender,
            x.Rating,
            x.MaxDiscount,
            Price(x.MinimumSalePrice),
            Price(x.MinimumMarketPrice),
            options.Value.PriceUnit, Id(x.StateId), Id(x.CityId))
        ).ToArray();

        return new(
            items,
            q.Page,
            q.Limit,
            Checked(r.SortList ?? []).Select(x => new PoolSort(Required(x.Code), Required(x.Title))
        ).ToArray(), r.SortedBy, r.TotalPages);
    }

    public async Task<IReadOnlyList<PoolSession>> SessionsAsync(string poolId, CancellationToken ct)
    {
        var id = NumericId(poolId);
        var result = new Dictionary<string, PoolSession>();
        foreach (var gender in new[] { 0, 1 })
        {
            var response = await client.PostOrganizationAsync<OrganizationSessionsResponse>("saens/list", new()
            {
                ["pool"] = id, ["gender"] = gender
            }, ct);
            var observed = new HashSet<long>();
            foreach (var row in Checked(response.Saens))
            {
                if (!observed.Add(row.SaensRowId) || row.RemainCapacity is null or < 0) throw Invalid();
                var time = PoolSessionTime.ParseDateTimes(row.StartDatetime, row.FinishDatetime);
                // v1 has rows, not provider groups; an empty group key preserves that distinction.
                var session = new PoolSession("", Required(row.SaensName), gender,
                    [new PoolSessionRow(Id(row.SaensRowId), row.Status && row.StatusSell && row.RemainCapacity > 0,
                        row.AdultSaleStatus, row.ChildSaleStatus,
                        Price(row.PriceSaleAdult)!.Value, Price(row.PriceSaleChild)!.Value,
                        Price(row.PriceMarketAdult)!.Value, Price(row.PriceMarketChild)!.Value,
                        Required(row.StartDate), row.StartDay ?? "", Required(row.Time),
                        Html(row.Description, "html"), Html(row.TermsOfUse, "html"), options.Value.PriceUnit,
                        time.Start, time.End)]);
                var key = session.Rows[0].Id;
                if (result.TryGetValue(key, out var previous))
                {
                    if (previous.Name != session.Name || previous.Rows[0] != session.Rows[0]) throw Invalid();
                    // The same row returned by both gender queries is eligible for both.
                    result[key] = previous with { Gender = 2 };
                }
                else result.Add(key, session);
            }
        }
        return result.Values.ToArray();
    }

    public async Task<string> TermsAsync(string poolId, CancellationToken ct)
    {
        var id = NumericId(poolId);
        var r = await client.PostOrganizationAsync<PoolInfoResponse>($"pool/info/{id}", new(), ct);
        return Html((r.Pool ?? throw Invalid()).TermsOfUse, "html");
    }
    public async Task<PoolQuote> QuoteAsync(PoolSelection s, CancellationToken ct)
    {
        if (!options.Value.CalculateReadOnlyConfirmed)
            throw new PoolException("رفتار محاسبه قیمت تأمین‌کننده هنوز تأیید نشده است", 503);

        var sessions = await SessionsAsync(s.PoolId, ct);
        var row = sessions.SelectMany(x => x.Rows).SingleOrDefault(x => x.Id == s.SessionRowId)
            ?? throw new PoolException("سانس یافت نشد", 404);

        if (!row.IsSelling || s.AdultCount > 0 && !row.AdultEnabled || s.ChildCount > 0 && !row.ChildEnabled)
            throw new PoolException("فروش سانس غیرفعال است");

        var r = await WithRegisteredUserAsync(() => client.PostOrganizationAsync<QuoteResponse>("reservation/calculate", new()
        {
            ["saens_row_id"] = NumericId(s.SessionRowId),
            ["count_adult"] = s.AdultCount,
            ["count_child"] = s.ChildCount, ["mobile"] = Mobile()
        }, ct), ct);

        var c = r.Cart ?? throw Invalid();

        if (Id(c.SaensRowId) != s.SessionRowId || c.CountAdult != s.AdultCount || c.CountChild != s.ChildCount || c.TotalSale < 0 || c.TotalMarket < 0 || c.StartDate != row.Date || c.Time != row.Time)
            throw Invalid();

        return new(
            s.SessionRowId,
            c.CountAdult,
            c.CountChild,
            c.TotalSale,
            c.TotalMarket,
            ToMinor(c.TotalSale, options.Value.PriceUnit),
            options.Value.PriceUnit,
            Required(c.StartDate),
            c.StartDay ?? "",
            Required(c.Time), row.StartsAtUtc, row.EndsAtUtc
        );
    }

    private string Mobile()
    {
        var mobile = options.Value.UserMobile;
        if (mobile is null || mobile.Length != 11 || !mobile.StartsWith("09") || mobile.Any(c => c is < '0' or > '9'))
            throw new PoolException("شماره همراه ثابت رزرو تنظیم نشده است", 503, "POOLS_PROVIDER_USER_NOT_CONFIGURED");
        return mobile;
    }

    private async Task<T> WithRegisteredUserAsync<T>(Func<Task<T>> operation, CancellationToken ct)
    {
        try { return await operation(); }
        catch (PoolException ex) when (ex.Code == "POOLS_PROVIDER_USER_NOT_FOUND")
        {
            await RegisterUserAsync(ct);
            return await operation();
        }
    }

    public async Task RegisterUserAsync(CancellationToken ct)
    {
        var o = options.Value;
        if (string.IsNullOrWhiteSpace(o.UserName) || string.IsNullOrWhiteSpace(o.UserPassword)
            || string.IsNullOrWhiteSpace(o.UserState) || string.IsNullOrWhiteSpace(o.UserCity))
            throw new PoolException("اطلاعات کاربر ثابت رزرو کامل نیست", 503, "POOLS_PROVIDER_USER_NOT_CONFIGURED");
        await client.PostOrganizationAsync<WireResponse>("auth/register", new()
        { ["name"] = o.UserName, ["mobile"] = Mobile(), ["password"] = o.UserPassword,
          ["state_id"] = NumericId(o.UserState), ["city_id"] = NumericId(o.UserCity) }, ct);
    }

    public async Task<IReadOnlyList<PoolArea>> AreasAsync(string cityId, CancellationToken ct)
    {
        var r = await client.PostOrganizationAsync<AreasResponse>("area/list", new() { ["city_id"] = NumericId(cityId) }, ct);
        return Checked(r.Areas).Select(x => new PoolArea(Id(x.AreaId), Id(x.CityId), Required(x.AreaName))).ToArray();
    }

    public async Task<PoolProviderDetails> DetailsAsync(string poolId, CancellationToken ct)
    {
        var r = await client.PostOrganizationAsync<PoolInfoResponse>($"pool/info/{NumericId(poolId)}", new(), ct);
        var p = r.Pool ?? throw Invalid();
        if (Id(p.PoolId) != poolId) throw Invalid();
        return new(poolId, Html(p.Description, "html"), Html(p.TermsOfUse, "html"), p.Address ?? "", p.Tel,
            (p.Images ?? []).Select(SafeUrl).OfType<string>().ToArray(), p.Attributes ?? []);
    }

    public async Task<PoolSessionAvailability> SessionInfoAsync(string poolId, string rowId, CancellationToken ct)
    {
        var r = await client.PostOrganizationAsync<SessionInfoResponse>("saens/info", new()
        { ["pool"] = NumericId(poolId), ["saens_row_id"] = NumericId(rowId) }, ct);
        var row = r.SaensRow ?? throw Invalid();
        if (Id(row.SaensRowId) != rowId) throw Invalid();
        var time = PoolSessionTime.ParseDateTimes(row.StartDatetime, row.FinishDatetime);
        return new(rowId, row.StatusSell && row.RemainCapacity, time.Start, time.End);
    }

    public async Task<PoolExternalReservation> ReserveAsync(PoolSelection selection, CancellationToken ct)
    {
        // Only an explicit not_found_user rejection permits a second reserve request.
        var r = await WithRegisteredUserAsync(() => client.PostOrganizationAsync<ReservationResponse>("reservation/reserve", new()
        { ["saens_row_id"] = NumericId(selection.SessionRowId), ["count_adult"] = selection.AdultCount,
          ["count_child"] = selection.ChildCount, ["mobile"] = Mobile() }, ct), ct);
        if (r.ReservationStatus != "reserved") throw Invalid();
        return Reservation(r.Reservation ?? throw Invalid());
    }

    public async Task<PoolExternalReservation> ReservationInfoAsync(string reservationId, CancellationToken ct)
    {
        var r = await client.PostOrganizationAsync<ReservationResponse>("reservation/info", new()
        { ["reservation_id"] = NumericId(reservationId), ["mobile"] = Mobile() }, ct);
        var result = Reservation(r.Reservation ?? throw Invalid());
        if (result.Id != reservationId) throw Invalid();
        return result;
    }

    public async Task<PoolReservationHistory> ReservationHistoryAsync(int page, int limit, CancellationToken ct)
    {
        if (page < 1 || limit is < 1 or > 100) throw new PoolException("صفحه معتبر نیست", 400);
        var r = await client.PostOrganizationAsync<ReservationHistoryResponse>($"reservation/history/{page}/{limit}",
            new() { ["mobile"] = Mobile() }, ct);
        if (r.Page != page || r.Limit != limit) throw Invalid();
        return new(page, limit, Checked(r.Reservations).Select(Reservation).ToArray());
    }

    private static PoolExternalReservation Reservation(ReservationWire r)
    {
        var voucher = r.Voucher.ValueKind switch
        { System.Text.Json.JsonValueKind.String => r.Voucher.GetString(), System.Text.Json.JsonValueKind.Number => r.Voucher.GetRawText(), _ => null };
        if (r.CountAdult < 0 || r.CountChild < 0 || r.UserPaid < 0 || r.Enter is < 0 or > 1) throw Invalid();
        return new(Id(r.ReservationId), Required(voucher), Required(r.PoolName), Required(r.SaensName), r.CountAdult, r.CountChild,
            r.UserPaid, Required(r.StartDate), Required(r.Time), r.Enter);
    }

    public static long? ToMinor(decimal amount, string? unit)
    {
        if (amount < 0)
            throw Invalid();

        if (unit is not ("IRR" or "TOMAN"))
            return null;

        try
        {
            var value = checked(amount * (unit == "TOMAN" ? 10 : 1));
            if (value != decimal.Truncate(value))
                throw Invalid();

            return checked((long)value);
        }
        catch (OverflowException)
        {
            throw Invalid();
        }
    }

    public static string Html(string? value, string? type)
    {
        if (type != "html")
            return WebUtility.HtmlEncode(value ?? "");

        var sanitizer = new HtmlSanitizer();

        sanitizer.AllowedTags.Clear();
        sanitizer.AllowedTags.UnionWith(["p", "br", "strong", "b", "em", "i", "u", "ul", "ol", "li", "a", "blockquote"]);
        sanitizer.AllowedAttributes.Clear();
        sanitizer.AllowedAttributes.UnionWith(["href", "title"]);
        sanitizer.AllowedSchemes.Clear();
        sanitizer.AllowedSchemes.UnionWith(["http", "https"]);
        sanitizer.AllowDataAttributes = false;

        return sanitizer.Sanitize(value ?? "");
    }

    private static string? SafeUrl(string? s) =>
        Uri.TryCreate(s, UriKind.Absolute, out var uri) && uri.Scheme is "https" or "http"
            ? uri.AbsoluteUri
            : null;

    private static decimal? Price(decimal? p)
    {
        if (p < 0)
            throw Invalid();

        return p;
    }

    private static string Required(string? s) =>
        string.IsNullOrWhiteSpace(s)
            ? throw Invalid()
            : s;

    private static T[] Checked<T>(T[]? values) where T : class =>
        values != null && values.All(x => x != null)
            ? values
        : throw Invalid();

    private static string Id(long id) =>
        id > 0
            ? id.ToString(CultureInfo.InvariantCulture)
            : throw Invalid();

    private static long? OptionalId(string? id) =>
        string.IsNullOrWhiteSpace(id)
            ? null
            : NumericId(id);

    public static long NumericId(string id) =>
        long.TryParse(id, NumberStyles.None, CultureInfo.InvariantCulture, out var n) && n > 0
            ? n
            : throw new PoolException("شناسه تأمین‌کننده معتبر نیست", 400);

    private static PoolException Invalid() =>
        new("پاسخ تأمین‌کننده معتبر نیست", 502);
}
