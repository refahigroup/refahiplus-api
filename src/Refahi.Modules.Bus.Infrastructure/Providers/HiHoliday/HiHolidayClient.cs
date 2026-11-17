using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Refahi.Modules.Bus.Application.Contracts;

namespace Refahi.Modules.Bus.Infrastructure.Providers.HiHoliday;

public sealed class HiHolidayOptions
{
    public Uri? BaseUrl { get; set; }
    public string ApiKey { get; set; } = "";
    public string Username { get; set; } = "";
    public string Password { get; set; } = "";
}

public sealed record HiHolidayPassenger(string FirstName, string LastName, string Gender,
    string? PhoneNumber, string? Email, string? NationalCode);

public sealed class HiHolidayClient(HttpClient http, IOptions<HiHolidayOptions> options, ILogger<HiHolidayClient> logger)
{
    private readonly HiHolidayOptions _settings = options.Value;

    public Task<JsonElement> RegionsAsync(CancellationToken ct) => SendAsync("Regions", null, ct);

    public Task<JsonElement> SearchAsync(int origin, int destination, DateOnly date, CancellationToken ct) =>
        SendAsync("Search", new { DepartureID = origin, ArrivalID = destination,
            DepartureDate = date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) }, ct);

    public Task<JsonElement> DetailAsync(long code, IReadOnlyList<int>? seats, CancellationToken ct) =>
        SendAsync("Detail", seats is null ? (object)new { Code = code } : new { Code = code, Seats = seats }, ct);

    public Task<JsonElement> RevalidateAsync(long code, IReadOnlyList<int> seats, CancellationToken ct)
    {
        EnsureMerchantCredentials();
        return SendAsync("Revalidate", new { _settings.Username, _settings.Password, Code = code, Seats = seats }, ct);
    }

    public Task<JsonElement> PreReserveAsync(Guid sessionId, CancellationToken ct) =>
        SendAsync("PreReserve", new { SessionID = sessionId }, ct);

    public Task<JsonElement> BookAsync(string requestNumber, IReadOnlyList<HiHolidayPassenger> passengers, CancellationToken ct) =>
        SendAsync("Book", new { RequestNumber = requestNumber, RequestPassengers = passengers }, ct);

    public Task<JsonElement> ReserveAsync(string requestNumber, CancellationToken ct) =>
        SendAsync("Reserve", new { RequestNumber = requestNumber }, ct);

    public Task<JsonElement> BookCancelAsync(string requestNumber, CancellationToken ct) =>
        SendAsync("BookCancel", new { RequestNumber = requestNumber }, ct);

    public Task<JsonElement> CancelInfoAsync(string requestNumber, CancellationToken ct) =>
        SendAsync("CancelInfo", new { RequestNumber = requestNumber }, ct);

    public Task<JsonElement> CancelAsync(string requestNumber, CancellationToken ct) =>
        SendAsync("Cancel", new { RequestNumber = requestNumber }, ct);

    public Task<JsonElement> RequestsAsync(int page, int size, CancellationToken ct) =>
        SendAsync("Requests", new { createBy = _settings.Username,
            PageSize = size.ToString(CultureInfo.InvariantCulture), PageNumber = page.ToString(CultureInfo.InvariantCulture) }, ct);

    public Task<JsonElement> RequestDetailAsync(string requestNumber, CancellationToken ct) =>
        SendAsync("Request/detail", new { createBy = _settings.Username, RequestNumber = requestNumber }, ct);

    public Task<JsonElement> BalanceAsync(CancellationToken ct)
    {
        EnsureMerchantCredentials();
        return SendAsync("Balance", new { _settings.Username, _settings.Password }, ct);
    }

    private void EnsureMerchantCredentials()
    {
        if (string.IsNullOrWhiteSpace(_settings.Username) || string.IsNullOrWhiteSpace(_settings.Password))
            throw new BusRequestException("اطلاعات حساب خرید اتوبوس در تأمین‌کننده تنظیم نشده است.", 503);
    }

    public static long ToRials(decimal tomans)
    {
        var rials = checked(tomans * 10);
        if (rials < 0 || rials != decimal.Truncate(rials))
            throw new FormatException("مبلغ تأمین‌کننده معتبر نیست.");
        return checked((long)rials);
    }

    private async Task<JsonElement> SendAsync(string operation, object? payload, CancellationToken ct)
    {
        var baseUrl = _settings.BaseUrl;
        if (baseUrl is not { IsAbsoluteUri: true, Scheme: "https", IsLoopback: false }
            || !string.IsNullOrEmpty(baseUrl.UserInfo) || !string.IsNullOrEmpty(baseUrl.Query)
            || !string.IsNullOrEmpty(baseUrl.Fragment) || string.IsNullOrWhiteSpace(_settings.ApiKey))
            throw new BusRequestException("تنظیمات اتصال تأمین‌کننده آماده نیست.", 503);

        var path = operation switch
        {
            "Balance" => "v4/Member/Balance",
            "Search" => "V4/Bus/Search",
            _ => $"v4/Bus/{operation}"
        };
        var uri = new Uri($"{baseUrl.AbsoluteUri.TrimEnd('/')}/{path}/{Uri.EscapeDataString(_settings.ApiKey)}");
        using var request = new HttpRequestMessage(HttpMethod.Post, uri);
        request.Content = payload is null
            ? new StringContent("", System.Text.Encoding.UTF8, "application/json")
            : JsonContent.Create(payload, options: new JsonSerializerOptions
            {
                DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
            });

        var started = System.Diagnostics.Stopwatch.GetTimestamp();
        try
        {
            using var response = await http.SendAsync(request, ct);
            logger.LogInformation("HiHoliday {Operation} HTTP {Status} in {ElapsedMs} ms",
                operation, (int)response.StatusCode, System.Diagnostics.Stopwatch.GetElapsedTime(started).TotalMilliseconds);
            if (!response.IsSuccessStatusCode)
                throw new BusRequestException("پاسخ تأمین‌کننده در دسترس نیست.", 502);
            return await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: ct);
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException or OperationCanceledException && !ct.IsCancellationRequested)
        {
            logger.LogWarning("HiHoliday {Operation} failed with {ErrorType}", operation, ex.GetType().Name);
            throw new BusRequestException("نتیجهٔ درخواست از تأمین‌کننده مشخص نیست.", 502);
        }
    }
}
