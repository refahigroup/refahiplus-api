using System.Diagnostics;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Refahi.Modules.Hotels.Application.Contracts.Catalogue;
namespace Refahi.Modules.Hotels.Infrastructure.Providers.Lamasoo;

public sealed class LamasooApiClient(HttpClient http, ILogger<LamasooApiClient> logger)
{
    internal static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower };
    internal async Task<T> SendAsync<T>(HttpMethod method, string path, object? body, bool mutation, CancellationToken ct)
    {
        using var message = new HttpRequestMessage(method, path);
        if (body is not null) message.Content = JsonContent.Create(body, options: Json);
        var started = Stopwatch.GetTimestamp();
        var outcome = "failure";
        try
        {
            using var response = await http.SendAsync(message, ct);
            logger.LogInformation("Hotel provider HTTP operation Provider={Provider} Path={Path} Status={Status} ElapsedMs={ElapsedMs}",
                "Lamasoo", path.Split('?')[0], (int)response.StatusCode, Stopwatch.GetElapsedTime(started).TotalMilliseconds);
            if (!response.IsSuccessStatusCode)
            {
                if (mutation && ((int)response.StatusCode >= 500 || response.StatusCode == System.Net.HttpStatusCode.RequestTimeout))
                    throw new HotelExternalOutcomeUnknownException("نتیجه عملیات رزرو هنوز مشخص نیست.");
                throw new InvalidOperationException("عملیات هتل توسط تأمین‌کننده پذیرفته نشد.");
            }
            var result = await response.Content.ReadFromJsonAsync<T>(Json, ct) ?? throw new JsonException();
            outcome = "success";
            return result;
        }
        catch (HotelExternalOutcomeUnknownException) { outcome = "unknown"; throw; }
        catch (Exception ex) when (mutation && ex is HttpRequestException or OperationCanceledException or JsonException)
        { outcome = "unknown"; throw new HotelExternalOutcomeUnknownException("نتیجه عملیات رزرو هنوز مشخص نیست.", ex); }
        finally { Catalogue.HotelProviderTelemetry.Record("Lamasoo", path.Split('?')[0], outcome, Stopwatch.GetElapsedTime(started).TotalMilliseconds); }
    }
}
