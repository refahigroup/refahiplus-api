using Microsoft.Extensions.Options;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace Refahi.Modules.Charge.Infrastructure.Providers.Eniac;

public sealed class EniacApiClient
{
    private readonly HttpClient _http; private readonly EniacOptions _options;
    private readonly SemaphoreSlim _tokenLock = new(1, 1);
    private string? _token; private DateTimeOffset _tokenExpiresAt;
    public EniacApiClient(HttpClient http, IOptions<EniacOptions> options) { _http = http; _options = options.Value; }

    public Task<JsonDocument> GetAsync(string path, CancellationToken ct) => SendAsync(HttpMethod.Get, path, null, true, ct);
    public Task<JsonDocument> PostAsync(string path, object body, bool safeToRetry, CancellationToken ct) => SendAsync(HttpMethod.Post, path, body, safeToRetry, ct);

    private async Task<JsonDocument> SendAsync(HttpMethod method, string path, object? body, bool safeToRetry, CancellationToken ct)
    {
        var attempts = safeToRetry ? 3 : 1;

        for (var attempt = 1; ; attempt++)
        {
            var token = await GetTokenAsync(false, ct);
            using var request = new HttpRequestMessage(method, path);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

            if (body is not null) 
                request.Content = JsonContent.Create(body);

            try
            {
                using var response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);

                if (response.StatusCode == HttpStatusCode.Unauthorized && attempt == 1)
                {
                    await GetTokenAsync(true, ct);
                    attempts = Math.Max(attempts, 2);
                    continue;
                }

                var stream = await response.Content.ReadAsStreamAsync(ct);
                
                if (!response.IsSuccessStatusCode)
                    throw new HttpRequestException($"Eniac HTTP {(int)response.StatusCode}", null, response.StatusCode);

                return await JsonDocument.ParseAsync(stream, cancellationToken: ct);
            }
            catch (Exception) when (safeToRetry && attempt < attempts && !ct.IsCancellationRequested)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(200 * attempt), ct);
            }
        }
    }

    private async Task<string> GetTokenAsync(bool force, CancellationToken ct)
    {
        if (!force && !string.IsNullOrWhiteSpace(_token) &&
            _tokenExpiresAt > DateTimeOffset.UtcNow.AddSeconds(_options.TokenRefreshSkewSeconds)) return _token;
        await _tokenLock.WaitAsync(ct);
        try
        {
            if (!force && !string.IsNullOrWhiteSpace(_token) &&
                _tokenExpiresAt > DateTimeOffset.UtcNow.AddSeconds(_options.TokenRefreshSkewSeconds)) return _token;
            using var response = await _http.PostAsJsonAsync("/api/Account/GetToken", new { password = _options.Password, userName = _options.Username }, ct);
            response.EnsureSuccessStatusCode();
            using var doc = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(ct), cancellationToken: ct);
            var data = doc.RootElement.GetProperty("data");
            _token = data.GetProperty("token").GetString() ?? throw new InvalidOperationException("توکن تامین‌کننده دریافت نشد");
            _tokenExpiresAt = data.TryGetProperty("expiration", out var expiration) && DateTimeOffset.TryParse(expiration.GetString(), out var parsed)
                ? parsed : DateTimeOffset.UtcNow.AddMinutes(10);
            return _token;
        }
        finally { _tokenLock.Release(); }
    }
}
