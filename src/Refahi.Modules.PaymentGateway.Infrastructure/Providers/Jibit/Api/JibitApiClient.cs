using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Refahi.Modules.PaymentGateway.Infrastructure.Providers.Jibit.Config;
using Refahi.Modules.PaymentGateway.Infrastructure.Providers.Jibit.Contract;
using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Refahi.Modules.PaymentGateway.Infrastructure.Providers.Jibit.Api;

/// <summary>
/// HTTP client wrapper for Jibit PPG v3 API.
/// Handles token generation, refresh, and caching automatically.
///
/// Token lifecycle (per Jibit docs):
///   accessToken  → valid ~24 hours (cached for 23h55m)
///   refreshToken → valid ~48 hours (cached for 47h55m)
///   On 401 / security.auth_required → force-refresh and retry once
/// </summary>
public class JibitApiClient
{
    private const string AccessTokenCacheKey = "jibit:access_token";
    private const string RefreshTokenCacheKey = "jibit:refresh_token";
    private static readonly TimeSpan AccessTokenCacheDuration = TimeSpan.FromHours(23).Add(TimeSpan.FromMinutes(55));
    private static readonly TimeSpan RefreshTokenCacheDuration = TimeSpan.FromHours(47).Add(TimeSpan.FromMinutes(55));

    private readonly HttpClient _http;
    private readonly JibitOptions _options;
    private readonly IMemoryCache _cache;
    private readonly ILogger<JibitApiClient> _logger;

    public JibitApiClient(
        HttpClient http,
        IOptions<JibitOptions> options,
        IMemoryCache cache,
        ILogger<JibitApiClient> logger)
    {
        _http = http;
        _options = options.Value;
        _cache = cache;
        _logger = logger;
    }

    /// <summary>
    /// ایجاد درخواست پرداخت جدید در جیبیت.
    /// بازگشت: purchaseId + pspSwitchingUrl
    /// </summary>
    public async Task<JibitCreatePurchaseResponse> CreatePurchaseAsync(JibitCreatePurchaseRequest request, CancellationToken ct)
    {
        await EnsureTokenAsync(ct);
        return await SendWithBearerAsync<JibitCreatePurchaseRequest, JibitCreatePurchaseResponse>(
            HttpMethod.Post, "/purchases", request, isRetry: false, ct);
    }

    /// <summary>
    /// تأیید تراکنش جیبیت با استفاده از purchaseId.
    /// </summary>
    public async Task<JibitVerifyResponse> VerifyPurchaseAsync(string purchaseId, CancellationToken ct)
    {
        await EnsureTokenAsync(ct);
        return await SendGetWithBearerAsync<JibitVerifyResponse>(
            $"/purchases/{purchaseId}/verify", isRetry: false, ct);
    }

    // ─────────────────────────────────────────────────────────
    // Token Management
    // ─────────────────────────────────────────────────────────

    private async Task EnsureTokenAsync(CancellationToken ct)
    {
        // Token is already cached and valid
        if (_cache.TryGetValue(AccessTokenCacheKey, out string? _))
            return;

        // Refresh token exists → try to refresh
        if (_cache.TryGetValue(RefreshTokenCacheKey, out string? cachedRefreshToken) &&
            !string.IsNullOrEmpty(cachedRefreshToken))
        {
            try
            {
                var currentAccess = _cache.Get<string>(AccessTokenCacheKey) ?? string.Empty;
                var refreshRequest = new JibitRefreshTokenRequest
                {
                    AccessToken = currentAccess,
                    RefreshToken = cachedRefreshToken
                };
                var refreshResponse = await _http.PostAsJsonAsync("/tokens/refresh", refreshRequest, ct);
                if (refreshResponse.IsSuccessStatusCode)
                {
                    var tokenResult = await refreshResponse.Content.ReadFromJsonAsync<JibitTokenResponse>(ct);
                    if (tokenResult != null && !string.IsNullOrEmpty(tokenResult.AccessToken))
                    {
                        StoreTokens(tokenResult.AccessToken, tokenResult.RefreshToken);
                        _logger.LogInformation("Jibit: Token refreshed successfully.");
                        return;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Jibit: Token refresh failed. Generating new token.");
            }
        }

        // Generate completely new token
        await GenerateNewTokenAsync(ct);
    }

    private async Task GenerateNewTokenAsync(CancellationToken ct)
    {
        _logger.LogInformation("Jibit: Generating new access token.");

        var tokenRequest = new JibitTokenRequest
        {
            ApiKey = _options.ApiKey,
            SecretKey = _options.SecretKey
        };

        var response = await _http.PostAsJsonAsync("/tokens", tokenRequest, ct);

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(ct);
            _logger.LogError("Jibit: Token generation failed. Status={Status} Body={Body}", response.StatusCode, body);
            throw new InvalidOperationException($"جیبیت: خطا در دریافت توکن. Status={response.StatusCode}");
        }

        var tokenResult = await response.Content.ReadFromJsonAsync<JibitTokenResponse>(ct)
            ?? throw new InvalidOperationException("جیبیت: پاسخ توکن خالی بود.");

        if (string.IsNullOrEmpty(tokenResult.AccessToken))
            throw new InvalidOperationException("جیبیت: accessToken دریافتی خالی است.");

        StoreTokens(tokenResult.AccessToken, tokenResult.RefreshToken);
        _logger.LogInformation("Jibit: New token generated and cached.");
    }

    private void StoreTokens(string accessToken, string refreshToken)
    {
        _cache.Set(AccessTokenCacheKey, accessToken, AccessTokenCacheDuration);
        if (!string.IsNullOrEmpty(refreshToken))
            _cache.Set(RefreshTokenCacheKey, refreshToken, RefreshTokenCacheDuration);
    }

    // ─────────────────────────────────────────────────────────
    // HTTP helpers with Bearer token and auto-retry on 401
    // ─────────────────────────────────────────────────────────

    private async Task<TResponse> SendWithBearerAsync<TRequest, TResponse>(
        HttpMethod method, string path, TRequest body, bool isRetry, CancellationToken ct)
        where TResponse : class, new()
    {
        var accessToken = _cache.Get<string>(AccessTokenCacheKey) ?? string.Empty;
        using var request = new HttpRequestMessage(method, path);
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
        request.Content = JsonContent.Create(body);

        var response = await _http.SendAsync(request, ct);

        if (response.StatusCode == HttpStatusCode.Unauthorized && !isRetry)
        {
            _logger.LogWarning("Jibit: Received 401. Forcing token regeneration and retrying.");
            _cache.Remove(AccessTokenCacheKey);
            await GenerateNewTokenAsync(ct);
            return await SendWithBearerAsync<TRequest, TResponse>(method, path, body, isRetry: true, ct);
        }

        if (!response.IsSuccessStatusCode)
        {
            var bodyText = await response.Content.ReadAsStringAsync(ct);
            _logger.LogError("Jibit: Request to {Path} failed. Status={Status} Body={Body}", path, response.StatusCode, bodyText);
            throw new HttpRequestException($"جیبیت: درخواست به {path} ناموفق بود. Status={response.StatusCode}");
        }

        return await response.Content.ReadFromJsonAsync<TResponse>(ct)
               ?? new TResponse();
    }

    private async Task<TResponse> SendGetWithBearerAsync<TResponse>(
        string path, bool isRetry, CancellationToken ct)
        where TResponse : class, new()
    {
        var accessToken = _cache.Get<string>(AccessTokenCacheKey) ?? string.Empty;
        using var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

        var response = await _http.SendAsync(request, ct);

        if (response.StatusCode == HttpStatusCode.Unauthorized && !isRetry)
        {
            _logger.LogWarning("Jibit: Received 401 on GET {Path}. Forcing token regeneration and retrying.", path);
            _cache.Remove(AccessTokenCacheKey);
            await GenerateNewTokenAsync(ct);
            return await SendGetWithBearerAsync<TResponse>(path, isRetry: true, ct);
        }

        if (!response.IsSuccessStatusCode)
        {
            var bodyText = await response.Content.ReadAsStringAsync(ct);
            _logger.LogError("Jibit: GET {Path} failed. Status={Status} Body={Body}", path, response.StatusCode, bodyText);
            throw new HttpRequestException($"جیبیت: درخواست GET به {path} ناموفق بود. Status={response.StatusCode}");
        }

        return await response.Content.ReadFromJsonAsync<TResponse>(ct)
               ?? new TResponse();
    }
}
