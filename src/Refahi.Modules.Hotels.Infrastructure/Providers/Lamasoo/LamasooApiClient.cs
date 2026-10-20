using System.Diagnostics;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Refahi.Modules.Hotels.Application.Contracts.Catalogue;
namespace Refahi.Modules.Hotels.Infrastructure.Providers.Lamasoo;

public sealed class LamasooApiClient(HttpClient http, ILogger<LamasooApiClient> logger, IOptions<HotelLifecycleOptions>? lifecycle = null,
    IOptions<LamasooOptions>? options = null)
{
    internal static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower };
    internal async Task<T> SendAsync<T>(HttpMethod method, string path, object? body, bool mutation, CancellationToken ct)
    {
        var policy = lifecycle?.Value ?? new();
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct);
        deadline.CancelAfter(TimeSpan.FromSeconds(mutation
            ? path.EndsWith("booking_submit", StringComparison.Ordinal) ? policy.SubmitTimeoutSeconds : policy.ConfirmTimeoutSeconds
            : options?.Value.TimeoutSeconds ?? 8));
        ct = deadline.Token;
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
                if (mutation)
                {
                    if (response.StatusCode is System.Net.HttpStatusCode.BadRequest or System.Net.HttpStatusCode.UnprocessableEntity)
                    {
                        try
                        {
                            using var error = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
                            if (error.RootElement.ValueKind == JsonValueKind.Object && error.RootElement.TryGetProperty("code", out var field)
                                && field.ValueKind == JsonValueKind.Number && field.TryGetInt32(out var code))
                            {
                                var submit = path.EndsWith("booking_submit", StringComparison.Ordinal);
                                var mapped = code switch
                                {
                                    3 or 8 or 9 or 11 => "OFFER_UNAVAILABLE",
                                    6 => "PROVIDER_CONTRACT_UNAVAILABLE",
                                    13 => "PROVIDER_CREDIT_UNAVAILABLE",
                                    15 when !submit => "SUPPLIER_INVOICE_UNPAID",
                                    21 when !submit => "SUBMIT_EXPIRED",
                                    21 or 22 when submit && error.RootElement.TryGetProperty("details", out _) => "OFFER_CONSTRAINT",
                                    _ => null
                                };
                                if (mapped is not null) throw new HotelProviderRejectedException(mapped, error.RootElement.GetRawText());
                            }
                        }
                        catch (JsonException) { }
                    }
                    throw new HotelExternalOutcomeUnknownException("نتیجه عملیات رزرو نیاز به بررسی دارد.");
                }
                throw new InvalidOperationException("عملیات هتل توسط تأمین‌کننده پذیرفته نشد.");
            }
            var result = await response.Content.ReadFromJsonAsync<T>(Json, ct) ?? throw new JsonException();
            outcome = "success";
            return result;
        }
        catch (JsonException ex) when (!mutation)
        {
            logger.LogWarning("Hotel provider response malformed Provider={Provider} Path={Path} JsonPath={JsonPath}",
                "Lamasoo", path.Split('?')[0], ex.Path);
            throw;
        }
        catch (HotelExternalOutcomeUnknownException) { outcome = "unknown"; throw; }
        catch (Exception ex) when (mutation && ex is HttpRequestException or OperationCanceledException or JsonException)
        { outcome = "unknown"; throw new HotelExternalOutcomeUnknownException("نتیجه عملیات رزرو هنوز مشخص نیست.", ex); }
        finally { Catalogue.HotelProviderTelemetry.Record("Lamasoo", path.Split('?')[0], outcome, Stopwatch.GetElapsedTime(started).TotalMilliseconds); }
    }
}
