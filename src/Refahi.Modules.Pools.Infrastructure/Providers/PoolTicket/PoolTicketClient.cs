using System.Diagnostics;
using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Polly.CircuitBreaker;
using Refahi.Modules.Pools.Application.Contracts;

namespace Refahi.Modules.Pools.Infrastructure.Providers.PoolTicket;
public sealed class PoolTicketOptions
{
    public string BaseUrl { get; set; } = "https://staging.poolticket.org";
    public string ClientId { get; set; } = "";
    public int TimeoutSeconds { get; set; } = 30;
    public string RequestEncoding { get; set; } = "Form";
    public string? PriceUnit { get; set; }
    public bool CalculateReadOnlyConfirmed { get; set; }
}
public sealed class PoolTicketClient(HttpClient http, IOptions<PoolTicketOptions> options, ILogger<PoolTicketClient> logger)
{
    internal static readonly JsonSerializerOptions Json = new() { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower };
    internal async Task<T> PostAsync<T>(string path, Dictionary<string, object?> fields, CancellationToken ct) where T : WireResponse
    {
        var settings = options.Value;
        if (string.IsNullOrWhiteSpace(settings.ClientId)) throw new PoolException("اتصال تأمین‌کننده استخر تنظیم نشده است", 503);
        var body = fields.Where(x => x.Value != null).ToDictionary(x => x.Key, x => x.Value!);
        body.Add("client_id", settings.ClientId);
        using var request = new HttpRequestMessage(HttpMethod.Post, "api/v2/" + path);
        request.Content = settings.RequestEncoding == "Json" ? JsonContent.Create(body) : new FormUrlEncodedContent(body.Select(x =>
            new KeyValuePair<string, string>(x.Key, x.Value is bool b ? (b ? "true" : "false") : Convert.ToString(x.Value, CultureInfo.InvariantCulture)!)));
        var timer = Stopwatch.StartNew();
        int? httpStatus = null; var successful = false;
        try
        {
            using var response = await http.SendAsync(request, ct);
            httpStatus = (int)response.StatusCode;
            T? result;
            try { result = await response.Content.ReadFromJsonAsync<T>(Json, ct); }
            catch (JsonException) { throw new PoolException("پاسخ تأمین‌کننده معتبر نیست", 502); }
            if (result == null) throw new PoolException("پاسخ تأمین‌کننده خالی است", 502);
            if (!response.IsSuccessStatusCode || result.Status != 1) throw MapError(result.Error);
            successful = true;
            return result;
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested) { throw new PoolException("مهلت پاسخ تأمین‌کننده پایان یافت", 504); }
        catch (HttpRequestException) { throw new PoolException("ارتباط با تأمین‌کننده برقرار نشد", 502); }
        catch (BrokenCircuitException) { throw new PoolException("سرویس استخر موقتاً در دسترس نیست", 503); }
        finally
        {
            logger.LogInformation("PoolTicket operation completed. Operation={Operation} HttpStatus={HttpStatus} LatencyMs={LatencyMs} Success={Success} TraceId={TraceId}",
                path.Split('/')[0], httpStatus, timer.ElapsedMilliseconds, successful, Activity.Current?.TraceId.ToString());
        }
    }
    public static PoolException MapError(string? error) => error switch
    {
        "invalid_pool" or "invalid_saens_row" => new("استخر یا سانس یافت نشد", 404),
        "invalid_state" => new("استان معتبر نیست", 400),
        "pool_capacity_overflow" or "not_available_ticket_serial" => new("ظرفیت بلیط انتخابی کافی نیست", 409),
        "coupon_error" => new("کد تخفیف قابل استفاده نیست", 400),
        "city_id,area_id and lat,lng should not be sent together" => new("شهر و مختصات همزمان مجاز نیستند", 400),
        "invalid_client" => new("اتصال تأمین‌کننده نیازمند بررسی است", 502),
        _ => new("تأمین‌کننده درخواست را تکمیل نکرد؛ دوباره تلاش کنید", 502)
    };
}
internal class WireResponse { public int Status { get; set; } public string? Error { get; set; } }
internal sealed class StatesResponse : WireResponse { public StateWire[]? States { get; set; } }
internal sealed record StateWire(long StateId, string Name);
internal sealed class CitiesResponse : WireResponse { public CityWire[]? Cities { get; set; } }
internal sealed record CityWire(long CityId, string CityName, long StateId, string StateName);
internal sealed class CatalogResponse : WireResponse
{
    public int Page { get; set; } public int Limit { get; set; } public PoolWire[]? Pools { get; set; }
    public SortWire[]? SortList { get; set; } public string? SortedBy { get; set; }
}
internal sealed record SortWire(string Code, string Title);
internal sealed record PoolWire(long PoolId, string Name, string? DefaultImage, string StateName, string CityName, string? AreaName,
    decimal? Lat, decimal? Lng, bool StatusSell, int Gender, decimal? Rating, decimal? MaxDiscount,
    decimal? MinimumSalePrice, decimal? MinimumMarketPrice);
internal sealed class SessionsResponse : WireResponse { public SessionWire[]? Saens { get; set; } }
internal sealed record SessionWire(long SaensId, string SaensName, int Gender, RowWire[]? Rows);
internal sealed record RowWire(long SaensRowId, bool StatusSell, bool AdultSaleStatus, bool ChildSaleStatus,
    decimal PriceSaleAdult, decimal PriceSaleChild, decimal PriceMarketAdult, decimal PriceMarketChild,
    string StartDate, string StartDay, string Time, string? Description, string? DescriptionType, string? TermsOfUse, string? TermsOfUseType);
internal sealed class TermsResponse : WireResponse { public TermsWire? TermsOfUse { get; set; } }
internal sealed record TermsWire(string? Content);
internal sealed class QuoteResponse : WireResponse { public CartWire? Cart { get; set; } }
internal sealed record CartWire(long SaensRowId, decimal TotalSale, decimal TotalMarket, int CountAdult, int CountChild,
    string StartDate, string StartDay, string Time);
