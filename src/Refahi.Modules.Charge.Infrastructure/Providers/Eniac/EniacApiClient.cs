using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Refahi.Modules.Charge.Application.Contracts.Providers;
using Refahi.Modules.Charge.Domain.Aggregates;
using Refahi.Modules.Charge.Domain.Enums;
using Refahi.Modules.Charge.Domain.Repositories;
using Refahi.Modules.Charge.Infrastructure.Observability;
using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace Refahi.Modules.Charge.Infrastructure.Providers.Eniac;

public sealed class EniacApiClient
{
    private readonly HttpClient _http;
    private readonly EniacOptions _options;
    private readonly IProviderCallLogRepository _callLogs;
    private readonly ILogger<EniacApiClient> _logger;
    private readonly SemaphoreSlim _tokenLock = new(1, 1);
    private string? _token;
    private DateTimeOffset _tokenExpiresAt;

    public EniacApiClient(
        HttpClient http,
        IOptions<EniacOptions> options,
        IProviderCallLogRepository callLogs,
        ILogger<EniacApiClient> logger)
    {
        _http = http;
        _options = options.Value;
        _callLogs = callLogs;
        _logger = logger;
    }

    public EniacApiClient(HttpClient http, IOptions<EniacOptions> options, ILogger<EniacApiClient> logger)
        : this(http, options, NullProviderCallLogRepository.Instance, logger) { }

    public Task<JsonDocument> GetAsync(string path, CancellationToken ct, string? operation = null, ProviderCallContext? context = null) =>
        SendAsync(HttpMethod.Get, path, null, true, operation ?? OperationName(path), context, ct);

    public Task<JsonDocument> PostAsync(string path, object body, bool safeToRetry, CancellationToken ct,
        string? operation = null, ProviderCallContext? context = null) =>
        SendAsync(HttpMethod.Post, path, body, safeToRetry, operation ?? OperationName(path), context, ct);

    private async Task<JsonDocument> SendAsync(
        HttpMethod method, string path, object? body, bool safeToRetry, string operation,
        ProviderCallContext? context, CancellationToken ct)
    {
        var attempts = safeToRetry ? 3 : 1;
        var correlationId = context?.CorrelationId ?? Guid.NewGuid().ToString("N");
        var requestSnapshot = ProviderPayloadSanitizer.SanitizeObject(body);

        for (var attempt = 1; attempt <= attempts; attempt++)
        {
            var stopwatch = Stopwatch.StartNew();
            try
            {
                var token = await GetTokenAsync(false, operation, correlationId, context, attempt, ct);
                using var request = new HttpRequestMessage(method, path);
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
                if (body is not null) request.Content = JsonContent.Create(body);

                using var response = await _http.SendAsync(request, HttpCompletionOption.ResponseContentRead, ct);

                if (response.StatusCode == HttpStatusCode.Unauthorized && attempt == 1)
                {
                    await GetTokenAsync(true, operation, correlationId, context, attempt, ct);
                    attempts = Math.Max(attempts, 2);
                    continue;
                }

                var responseBody = await response.Content.ReadAsStringAsync(ct);
                if (!response.IsSuccessStatusCode)
                {
                    var retryable = safeToRetry && (int)response.StatusCode >= 500;
                    var kind = response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden
                        ? ChargeProviderFailureKind.Authentication
                        : ChargeProviderFailureKind.ProviderRejected;
                    var outcome = kind == ChargeProviderFailureKind.Authentication
                        ? ProviderCallOutcome.AuthenticationError
                        : ProviderCallOutcome.ProviderRejected;
                    var logId = await WriteLogAsync(context, operation, "Http", outcome, method.Method, path,
                        (int)response.StatusCode, null, null, retryable, attempt, correlationId,
                        nameof(HttpRequestException), $"Eniac HTTP {(int)response.StatusCode}", requestSnapshot,
                        ProviderPayloadSanitizer.SanitizeJson(responseBody), stopwatch.ElapsedMilliseconds, ct);

                    throw new ChargeProviderException(
                        "ارتباط با تامین‌کننده با پاسخ ناموفق مواجه شد", kind, operation, "Http",
                        correlationId, retryable, logId, (int)response.StatusCode);
                }

                JsonDocument document;
                try
                {
                    document = JsonDocument.Parse(responseBody);
                }
                catch (JsonException ex)
                {
                    var logId = await WriteLogAsync(context, operation, "Deserialize", ProviderCallOutcome.InvalidResponse,
                        method.Method, path, (int)response.StatusCode, null, null, safeToRetry, attempt, correlationId,
                        ex.GetType().FullName, ex.Message, requestSnapshot,
                        ProviderPayloadSanitizer.SanitizeJson(responseBody), stopwatch.ElapsedMilliseconds, ct);
                    throw new ChargeProviderException("پاسخ تامین‌کننده قابل پردازش نیست",
                        ChargeProviderFailureKind.InvalidResponse, operation, "Deserialize", correlationId,
                        safeToRetry, logId, (int)response.StatusCode, ex);
                }

                await LogProviderRejectionIfNeededAsync(document.RootElement, context, operation, method.Method, path,
                    requestSnapshot, responseBody, attempt, correlationId, stopwatch.ElapsedMilliseconds, ct);
                return document;
            }
            catch (ChargeProviderException) when (safeToRetry && attempt < attempts && !ct.IsCancellationRequested)
            {
                await DelayRetryAsync(attempt, ct);
            }
            catch (OperationCanceledException ex) when (!ct.IsCancellationRequested)
            {
                var logId = await WriteLogAsync(context, operation, "Transport", ProviderCallOutcome.Timeout,
                    method.Method, path, null, null, null, safeToRetry, attempt, correlationId,
                    ex.GetType().FullName, "مهلت ارتباط با تامین‌کننده پایان یافت", requestSnapshot, "{}",
                    stopwatch.ElapsedMilliseconds, CancellationToken.None);
                if (safeToRetry && attempt < attempts) { await DelayRetryAsync(attempt, ct); continue; }
                throw new ChargeProviderException("مهلت ارتباط با تامین‌کننده پایان یافت",
                    ChargeProviderFailureKind.Timeout, operation, "Transport", correlationId, safeToRetry,
                    logId, innerException: ex);
            }
            catch (OperationCanceledException ex) when (ct.IsCancellationRequested)
            {
                await WriteLogAsync(context, operation, "Transport", ProviderCallOutcome.Cancelled,
                    method.Method, path, null, null, null, false, attempt, correlationId,
                    ex.GetType().FullName, "عملیات لغو شد", requestSnapshot, "{}", stopwatch.ElapsedMilliseconds,
                    CancellationToken.None);
                throw;
            }
            catch (HttpRequestException ex)
            {
                var logId = await WriteLogAsync(context, operation, "Transport", ProviderCallOutcome.TransportError,
                    method.Method, path, (int?)ex.StatusCode, null, null, safeToRetry, attempt, correlationId,
                    ex.GetType().FullName, ex.Message, requestSnapshot, "{}", stopwatch.ElapsedMilliseconds,
                    CancellationToken.None);
                if (safeToRetry && attempt < attempts) { await DelayRetryAsync(attempt, ct); continue; }
                throw new ChargeProviderException("ارتباط با تامین‌کننده برقرار نشد",
                    ChargeProviderFailureKind.Transport, operation, "Transport", correlationId, safeToRetry,
                    logId, (int?)ex.StatusCode, ex);
            }
        }

        throw new InvalidOperationException("چرخه تلاش تامین‌کننده بدون نتیجه پایان یافت");
    }

    private async Task<string> GetTokenAsync(bool force, string operation, string correlationId,
        ProviderCallContext? context, int attempt, CancellationToken ct)
    {
        if (!force && TokenIsValid()) return _token!;
        await _tokenLock.WaitAsync(ct);
        try
        {
            if (!force && TokenIsValid()) return _token!;
            var sw = Stopwatch.StartNew();
            using var response = await _http.PostAsJsonAsync("/api/Account/GetToken", new
            {
                password = _options.Password,
                userName = _options.Username
            }, ct);
            var body = await response.Content.ReadAsStringAsync(ct);

            if (!response.IsSuccessStatusCode)
            {
                var logId = await WriteLogAsync(context, operation, "Token", ProviderCallOutcome.AuthenticationError,
                    "POST", "/api/Account/GetToken", (int)response.StatusCode, null, null, true, attempt,
                    correlationId, nameof(HttpRequestException), "دریافت توکن تامین‌کننده ناموفق بود", "{}", "{}",
                    sw.ElapsedMilliseconds, ct);
                throw new ChargeProviderException("احراز هویت تامین‌کننده ناموفق بود",
                    ChargeProviderFailureKind.Authentication, operation, "Token", correlationId, true,
                    logId, (int)response.StatusCode);
            }

            try
            {
                using var doc = JsonDocument.Parse(body);
                var data = doc.RootElement.GetProperty("data");
                _token = data.GetProperty("token").GetString() ??
                    throw new JsonException("Provider token is missing.");
                _tokenExpiresAt = data.TryGetProperty("expiration", out var expiration) &&
                    DateTimeOffset.TryParse(expiration.GetString(), out var parsed)
                    ? parsed
                    : DateTimeOffset.UtcNow.AddMinutes(10);
                return _token;
            }
            catch (Exception ex) when (ex is JsonException or KeyNotFoundException or InvalidOperationException)
            {
                var logId = await WriteLogAsync(context, operation, "Token", ProviderCallOutcome.InvalidResponse,
                    "POST", "/api/Account/GetToken", (int)response.StatusCode, null, null, true, attempt,
                    correlationId, ex.GetType().FullName, "ساختار پاسخ توکن تامین‌کننده معتبر نیست", "{}", "{}",
                    sw.ElapsedMilliseconds, ct);
                throw new ChargeProviderException("پاسخ احراز هویت تامین‌کننده معتبر نیست",
                    ChargeProviderFailureKind.InvalidResponse, operation, "Token", correlationId, true,
                    logId, (int)response.StatusCode, ex);
            }
        }
        finally
        {
            _tokenLock.Release();
        }
    }

    private bool TokenIsValid() => !string.IsNullOrWhiteSpace(_token) &&
        _tokenExpiresAt > DateTimeOffset.UtcNow.AddSeconds(_options.TokenRefreshSkewSeconds);

    private async Task LogProviderRejectionIfNeededAsync(JsonElement root, ProviderCallContext? context,
        string operation, string method, string path, string requestSnapshot, string responseBody,
        int attempt, string correlationId, long latency, CancellationToken ct)
    {
        if (!root.TryGetProperty("success", out var success) || success.ValueKind == JsonValueKind.True) return;
        var providerCode = root.TryGetProperty("eniacResultCode", out var code) && code.TryGetInt32(out var parsed)
            ? parsed : (int?)null;
        var operatorCode = root.TryGetProperty("operatorResultCode", out var op) ? op.ToString() : null;
        await WriteLogAsync(context, operation, "Provider", ProviderCallOutcome.ProviderRejected,
            method, path, 200, providerCode, operatorCode, false, attempt, correlationId, null,
            EniacJson.String(root, "message"), requestSnapshot,
            ProviderPayloadSanitizer.SanitizeJson(responseBody), latency, ct);
    }

    private async Task<Guid?> WriteLogAsync(ProviderCallContext? context, string operation, string stage,
        ProviderCallOutcome outcome, string method, string path, int? statusCode, int? providerCode,
        string? operatorCode, bool retryable, int attempt, string correlationId, string? exceptionType,
        string? errorMessage, string requestJson, string responseJson, long latency, CancellationToken ct)
    {
        ChargeMetrics.ProviderFailure("Eniac", operation, outcome, statusCode);
        try
        {
            var log = ProviderCallLog.Create(context?.ChargeRequestId, context?.OrderId, context?.SagaId,
                "Eniac", operation, stage, outcome, method, path, statusCode, providerCode, operatorCode,
                retryable, attempt, correlationId, exceptionType,
                ProviderPayloadSanitizer.SafeMessage(errorMessage), requestJson, responseJson, latency, DateTime.UtcNow);
            await _callLogs.AddAsync(log, ct);
            await _callLogs.SaveChangesAsync(ct);
            return log.Id;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Provider call audit persistence failed. Provider={Provider} Operation={Operation} CorrelationId={CorrelationId}",
                "Eniac", operation, correlationId);
            return null;
        }
    }

    private static Task DelayRetryAsync(int attempt, CancellationToken ct) =>
        Task.Delay(TimeSpan.FromMilliseconds(200 * attempt + Random.Shared.Next(25, 125)), ct);

    private static string OperationName(string path) => path.Trim('/').Replace('/', '.');

    private sealed class NullProviderCallLogRepository : IProviderCallLogRepository
    {
        public static readonly NullProviderCallLogRepository Instance = new();
        public Task AddAsync(ProviderCallLog log, CancellationToken ct = default) => Task.CompletedTask;
        public Task<IReadOnlyList<ProviderCallLog>> GetForChargeRequestAsync(Guid requestId, int skip, int take, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<ProviderCallLog>>([]);
        public Task<int> DeleteOlderThanAsync(DateTime cutoffUtc, int take, CancellationToken ct = default) => Task.FromResult(0);
        public Task SaveChangesAsync(CancellationToken ct = default) => Task.CompletedTask;
    }
}
