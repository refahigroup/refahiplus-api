using System.Diagnostics;
using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Polly.CircuitBreaker;
using Refahi.Modules.Pools.Application.Contracts;

namespace Refahi.Modules.Pools.Infrastructure.Providers.PoolTicket;

public sealed class PoolTicketOptions
{
    public string BaseUrl { get; set; } = "https://staging.poolticket.org";
    public string ClientId { get; set; } = "ClientId";
    public string? AccessToken { get; set; }
    public string? RefreshToken { get; set; }
    public string? Username { get; set; }
    public string? Password { get; set; }
    [Microsoft.Extensions.Configuration.ConfigurationKeyName("user_name")]
    public string? UserName { get; set; }
    [Microsoft.Extensions.Configuration.ConfigurationKeyName("user_mobile")]
    public string? UserMobile { get; set; }
    [Microsoft.Extensions.Configuration.ConfigurationKeyName("user_password")]
    public string? UserPassword { get; set; }
    [Microsoft.Extensions.Configuration.ConfigurationKeyName("user_state")]
    public string? UserState { get; set; }
    [Microsoft.Extensions.Configuration.ConfigurationKeyName("user_city")]
    public string? UserCity { get; set; }
    public int TimeoutSeconds { get; set; } = 30;
    public string RequestEncoding { get; set; } = "Form";
    public string? PriceUnit { get; set; } = "TOMAN";
    public bool CalculateReadOnlyConfirmed { get; set; }
}

public sealed class PoolTicketClient(HttpClient http, IOptions<PoolTicketOptions> options, ILogger<PoolTicketClient> logger, PoolTicketTokenState? tokenState = null)
{
    internal static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        Converters = { new PoolTicketBooleanConverter() }
    };

    private readonly PoolTicketTokenState tokens = tokenState ?? new();

    internal async Task<T> PostOrganizationAsync<T>(string path, Dictionary<string, object?> fields, CancellationToken ct) where T : WireResponse
    {
        fields = new(fields) { ["access_token"] = await TokenAsync(ct) };
        try { return await SendAsync<T>("api/v1/org/" + path, fields, ct); }
        catch (PoolException ex) when (ex.Code == "POOLS_PROVIDER_TOKEN_INVALID" && path != "reservation/reserve"
            && !string.IsNullOrWhiteSpace(options.Value.Username))
        {
            await tokens.Gate.WaitAsync(ct);
            try { if (tokens.AccessToken == (string)fields["access_token"]!) { tokens.AccessToken = null; tokens.IgnoreConfiguredToken = true; } }
            finally { tokens.Gate.Release(); }
            fields["access_token"] = await TokenAsync(ct);
            return await SendAsync<T>("api/v1/org/" + path, fields, ct);
        }
    }

    private async Task<string> TokenAsync(CancellationToken ct)
    {
        await tokens.Gate.WaitAsync(ct);
        try
        {
            if (!string.IsNullOrWhiteSpace(tokens.AccessToken)) return tokens.AccessToken;
            var o = options.Value;
            if (!tokens.IgnoreConfiguredToken && !string.IsNullOrWhiteSpace(o.AccessToken))
            { tokens.RefreshToken = o.RefreshToken; return tokens.AccessToken = o.AccessToken; }
            if (string.IsNullOrWhiteSpace(o.Username) || string.IsNullOrWhiteSpace(o.Password))
                throw new PoolException("اطلاعات ورود سازمانی تأمین‌کننده تنظیم نشده است", 503, "POOLS_PROVIDER_AUTH_NOT_CONFIGURED");
            var response = await SendAsync<TokenResponse>("api/v1/token", new()
            { ["grant_type"] = "password", ["username"] = o.Username, ["password"] = o.Password, ["scope"] = "organization" }, ct);
            if (string.IsNullOrWhiteSpace(response.AccessToken) || string.IsNullOrWhiteSpace(response.RefreshToken))
                throw new PoolException("پاسخ ورود تأمین‌کننده معتبر نیست", 502);
            tokens.RefreshToken = response.RefreshToken;
            return tokens.AccessToken = response.AccessToken;
        }
        finally { tokens.Gate.Release(); }
    }

    public async Task CheckTokenAsync(CancellationToken ct)
    {
        var token = await TokenAsync(ct);
        if (string.IsNullOrWhiteSpace(tokens.RefreshToken)) throw new PoolException("توکن بررسی سازمانی تنظیم نشده است", 503, "POOLS_PROVIDER_AUTH_NOT_CONFIGURED");
        await SendAsync<WireResponse>("api/v1/token", new()
        { ["grant_type"] = "check_token", ["access_token"] = token, ["refresh_token"] = tokens.RefreshToken,
          ["scope"] = "organization" }, ct);
    }

    private async Task<T> SendAsync<T>(string path, Dictionary<string, object?> fields, CancellationToken ct) where T : WireResponse
    {
        var settings = options.Value;

        if (string.IsNullOrWhiteSpace(settings.ClientId))
            throw new PoolException("اتصال تأمین‌کننده استخر تنظیم نشده است", 503);

        var body = fields
            .Where(x => x.Value != null)
            .ToDictionary(x => x.Key, x => x.Value!);

        body.Add("client_id", settings.ClientId);

        using var request = new HttpRequestMessage(HttpMethod.Post, path);
        request.Headers.Accept.ParseAdd("application/json");

        request.Content = settings.RequestEncoding == "Json"
            ? JsonContent.Create(body)
            : new FormUrlEncodedContent(body.Select(x =>
                new KeyValuePair<string, string>(
                    x.Key,
                    x.Value is bool b
                        ? (b ? "true" : "false")
                        : Convert.ToString(x.Value, CultureInfo.InvariantCulture)!
                )
             ));

        var timer = Stopwatch.StartNew();

        int? httpStatus = null;
        var successful = false;

        try
        {
            using var response = await http.SendAsync(request, ct);
            httpStatus = (int)response.StatusCode;
            if (response.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
            {
                logger.LogWarning("PoolTicket request rate limited. Operation={Operation} HttpStatus={HttpStatus}", path, httpStatus);
                throw new PoolException("تعداد درخواست‌ها به تأمین‌کننده استخر بیش از حد مجاز است؛ کمی بعد دوباره تلاش کنید", 429, "POOLS_PROVIDER_RATE_LIMITED");
            }
            if (httpStatus is >= 300 and < 400)
            {
                logger.LogWarning("PoolTicket API returned redirect. Operation={Operation} ProviderPoolId={ProviderPoolId} HttpStatus={HttpStatus}",
                    path, fields.GetValueOrDefault("pool_id"), httpStatus);
                throw new PoolException("سرویس تأمین‌کننده به‌جای پاسخ API تغییر مسیر داد", 502, "POOLS_PROVIDER_REDIRECT");
            }

            var mediaType = response.Content.Headers.ContentType?.MediaType;
            if (mediaType is "text/html" or "application/xhtml+xml")
            {
                logger.LogWarning("PoolTicket API returned HTML. Operation={Operation} ProviderPoolId={ProviderPoolId} HttpStatus={HttpStatus} ContentType={ContentType}",
                    path, fields.GetValueOrDefault("pool_id"), httpStatus, mediaType);
                throw new PoolException("سرویس تأمین‌کننده به‌جای اطلاعات استخر صفحه وب برگرداند", 502, "POOLS_PROVIDER_NON_JSON");
            }
            T? result;

            try
            {
                var json = await response.Content.ReadAsStringAsync(ct);
                result = JsonSerializer.Deserialize<T>(json, Json);
            }
            catch (JsonException ex)
            {
                logger.LogWarning("PoolTicket JSON response invalid. Operation={Operation} HttpStatus={HttpStatus} ContentType={ContentType} ResponseType={ResponseType} JsonPath={JsonPath} LineNumber={LineNumber} BytePosition={BytePosition}",
                    path, httpStatus, mediaType, typeof(T).Name, ex.Path, ex.LineNumber, ex.BytePositionInLine);
                throw new PoolException("پاسخ تأمین‌کننده معتبر نیست", 502);
            }
            catch (NotSupportedException)
            {
                throw new PoolException("پاسخ تأمین‌کننده معتبر نیست", 502);
            }

            if (result == null)
                throw new PoolException("پاسخ تأمین‌کننده خالی است", 502);

            if (!response.IsSuccessStatusCode || result.Status != 1)
            {
                logger.LogWarning("PoolTicket response rejected. Operation={Operation} HttpStatus={HttpStatus} ProviderStatus={ProviderStatus} ResponseType={ResponseType}",
                    path, httpStatus, result.Status, typeof(T).Name);
                throw MapError(result.Error);
            }

            successful = true;

            return result;
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            throw new PoolException("مهلت پاسخ تأمین‌کننده پایان یافت", 504);
        }
        catch (HttpRequestException)
        {
            throw new PoolException("ارتباط با تأمین‌کننده برقرار نشد", 502);
        }
        catch (BrokenCircuitException)
        {
            throw new PoolException("سرویس استخر موقتاً در دسترس نیست", 503);
        }
        finally
        {
            logger.LogInformation("PoolTicket operation completed. Operation={Operation} HttpStatus={HttpStatus} LatencyMs={LatencyMs} Success={Success} TraceId={TraceId}",
                path, httpStatus, timer.ElapsedMilliseconds, successful, Activity.Current?.TraceId.ToString());
        }
    }
    public static PoolException MapError(string? error) => error switch
    {
        "invalid_pool" or "invalid_saens_row" or "invalid_saens_row_id"
            => new("استخر یا سانس یافت نشد", 404, "POOLS_PROVIDER_SELECTION_INVALID"),
        "invalid_state" or "invalid_state_id" or "invalid_city" or "invalid_city_id"
            => new("استان معتبر نیست", 400),
        "pool_capacity_overflow" or "not_available_ticket_serial"
            => new("ظرفیت بلیط انتخابی کافی نیست", 409),
        "coupon_error"
            => new("کد تخفیف قابل استفاده نیست", 400),
        "city_id,area_id and lat,lng should not be sent together" or "not_alowed_to_send_state_or_city_with_lat_lng_together"
            => new("شهر و مختصات همزمان مجاز نیستند", 400),
        "invalid_access_token" or "expired_access_token"
            => new("توکن سازمانی تأمین‌کننده معتبر نیست یا منقضی شده است", 502, "POOLS_PROVIDER_TOKEN_INVALID"),
        "access_denied"
            => new("دسترسی به اطلاعات تأمین‌کننده مجاز نیست", 502, "POOLS_PROVIDER_ACCESS_DENIED"),
        "invalid_gender"
            => new("جنسیت سانس معتبر نیست", 400, "POOLS_INVALID_GENDER"),
        "not_found_user" => new("کاربر رزرو نزد تأمین‌کننده ثبت نشده است", 409, "POOLS_PROVIDER_USER_NOT_FOUND"),
        "organization_credit_is_low" => new("اعتبار سازمان نزد تأمین‌کننده کافی نیست", 503, "POOLS_PROVIDER_CREDIT_LOW"),
        "reservation_failed" => new("رزرو توسط تأمین‌کننده رد شد", 409, "POOLS_PROVIDER_RESERVATION_REJECTED"),
        "validation" or "invalid_name" or "invalid_mobile" or "invalid_password" or "invalid_code_meli"
            => new("اطلاعات کاربر رزرو نزد تأمین‌کننده معتبر نیست", 502, "POOLS_PROVIDER_USER_INVALID"),
        "invalid_client"
            => new("اتصال تأمین‌کننده نیازمند بررسی است", 502, "POOLS_PROVIDER_CLIENT_INVALID"),
        _ => new("تأمین‌کننده درخواست را تکمیل نکرد؛ دوباره تلاش کنید", 502)
    };
}
internal class WireResponse
{
    [JsonConverter(typeof(PoolTicketStatusConverter))]
    public int Status { get; set; }
    public string? Error { get; set; }
}

// PoolTicket represents sale flags as either JSON booleans or integer 0/1.
internal sealed class PoolTicketBooleanConverter : JsonConverter<bool>
{
    public override bool Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType is JsonTokenType.True or JsonTokenType.False)
            return reader.GetBoolean();

        if (reader.TokenType == JsonTokenType.Number && reader.TryGetInt32(out var value) && value is 0 or 1)
            return value == 1;

        throw new JsonException("PoolTicket sale flags must be booleans or integer 0/1.");
    }

    public override void Write(Utf8JsonWriter writer, bool value, JsonSerializerOptions options) =>
        writer.WriteBooleanValue(value);
}

internal sealed class StatesResponse : WireResponse
{
    public StateWire[]? States { get; set; }
}

internal sealed record StateWire(long StateId, string StateName);
internal sealed class CitiesResponse : WireResponse
{
    public CityWire[]? Cities { get; set; }
}

internal sealed record CityWire(long CityId, string CityName, long StateId, string StateName);

internal sealed class CatalogResponse : WireResponse
{
    public int Page { get; set; }
    public int Limit { get; set; }
    public int? TotalPages { get; set; }
    public PoolWire[]? Pools { get; set; }
    public SortWire[]? SortList { get; set; }
    public string? SortedBy { get; set; }
}

internal sealed record SortWire(string Code, string Title);

internal sealed record PoolWire(long PoolId, string Name, string? DefaultImage, string StateName, string CityName, string? AreaName,
    [property: JsonConverter(typeof(PoolTicketCoordinateConverter))] decimal? Lat, [property: JsonConverter(typeof(PoolTicketCoordinateConverter))] decimal? Lng, bool StatusSell, int Gender, decimal? Rating, [property: JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)] decimal? MaxDiscount,
    decimal? MinimumSalePrice, decimal? MinimumMarketPrice, long StateId, long CityId, string? Address, string? Description, string? TermsOfUse, string? Tel, string[]? Images, string[]? Attributes);

internal sealed class OrganizationSessionsResponse : WireResponse
{
    public OrganizationSessionWire[]? Saens { get; set; }
}

internal sealed record OrganizationSessionWire(long SaensRowId, string SaensName,
    bool Status, bool StatusSell, bool AdultSaleStatus, bool ChildSaleStatus,
    decimal PriceSaleAdult, decimal PriceSaleChild, decimal PriceMarketAdult, decimal PriceMarketChild,
    int? RemainCapacity, string StartDate, string StartDay, string Time,
    string? Description, string? TermsOfUse, string? StartDatetime, string? FinishDatetime);

internal sealed class TermsResponse : WireResponse
{
    public TermsWire? TermsOfUse { get; set; }
}

internal sealed record TermsWire(string? Content);

internal sealed class QuoteResponse : WireResponse
{
    public CartWire? Cart { get; set; }
}

internal sealed record CartWire(long SaensRowId, decimal TotalSale, decimal TotalMarket, int CountAdult, int CountChild,
    string StartDate, string StartDay, string Time, decimal? TotalSalePoolticket);

public sealed class PoolTicketTokenState
{
    internal readonly SemaphoreSlim Gate = new(1, 1);
    internal string? AccessToken;
    internal string? RefreshToken;
    internal bool IgnoreConfiguredToken;
}
internal sealed class TokenResponse : WireResponse
{
    public string? AccessToken { get; set; }
    public string? RefreshToken { get; set; }
}
internal sealed class PoolTicketStatusConverter : JsonConverter<int>
{
    public override int Read(ref Utf8JsonReader reader, Type type, JsonSerializerOptions options) => reader.TokenType switch
    {
        JsonTokenType.True => 1, JsonTokenType.False => 0,
        JsonTokenType.Number when reader.TryGetInt32(out var n) && n is 0 or 1 => n,
        _ => throw new JsonException("Invalid provider status")
    };
    public override void Write(Utf8JsonWriter writer, int value, JsonSerializerOptions options) => writer.WriteNumberValue(value);
}
internal sealed class PoolInfoResponse : WireResponse { public PoolWire? Pool { get; set; } }
internal sealed class AreasResponse : WireResponse { public AreaWire[]? Areas { get; set; } }
internal sealed record AreaWire(long AreaId, long CityId, string AreaName);
internal sealed class SessionInfoResponse : WireResponse { public OrganizationSessionInfoWire? SaensRow { get; set; } }
// The document describes a boolean; staging also returns numeric capacity. Normalize only availability.
internal sealed record OrganizationSessionInfoWire(long SaensRowId, string SaensName, [property: JsonConverter(typeof(PoolTicketAvailabilityConverter))] bool RemainCapacity,
    bool StatusSell, string StartDatetime, string FinishDatetime);
internal sealed class ReservationResponse : WireResponse
{
    public string? ReservationStatus { get; set; }
    public ReservationWire? Reservation { get; set; }
}
internal sealed class ReservationHistoryResponse : WireResponse
{
    public int Page { get; set; }
    [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
    public int Limit { get; set; }
    public ReservationWire[]? Reservations { get; set; }
}
internal sealed record ReservationWire(long ReservationId, string PoolName, string SaensName, decimal UserPaid,
    string StartDate, string StartDay, string Time, int CountAdult, int CountChild, int Gender,
    JsonElement Voucher, int Enter, bool Cancelable);

internal sealed class PoolTicketCoordinateConverter : JsonConverter<decimal?>
{
    public override decimal? Read(ref Utf8JsonReader reader, Type type, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null) return null;
        if (reader.TokenType == JsonTokenType.Number && reader.TryGetDecimal(out var n)) return n;
        if (reader.TokenType == JsonTokenType.String)
        {
            var text = reader.GetString();
            if (string.IsNullOrWhiteSpace(text)) return null;
            if (decimal.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var number)) return number;
        }
        throw new JsonException("Invalid coordinate");
    }
    public override void Write(Utf8JsonWriter writer, decimal? value, JsonSerializerOptions options)
    { if (value.HasValue) writer.WriteNumberValue(value.Value); else writer.WriteNullValue(); }
}

internal sealed class PoolTicketAvailabilityConverter : JsonConverter<bool>
{
    public override bool Read(ref Utf8JsonReader reader, Type type, JsonSerializerOptions options)
    {
        if (reader.TokenType is JsonTokenType.True or JsonTokenType.False) return reader.GetBoolean();
        if (reader.TokenType == JsonTokenType.Number && reader.TryGetInt32(out var n) && n >= 0) return n > 0;
        throw new JsonException("Invalid session availability");
    }
    public override void Write(Utf8JsonWriter writer, bool value, JsonSerializerOptions options) => writer.WriteBooleanValue(value);
}
