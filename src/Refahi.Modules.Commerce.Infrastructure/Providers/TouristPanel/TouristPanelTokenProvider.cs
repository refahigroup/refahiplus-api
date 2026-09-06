using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;

namespace Refahi.Modules.Commerce.Infrastructure.Providers.TouristPanel;

public sealed class TouristPanelTokenProvider(HttpClient http, IOptions<TouristPanelOptions> options)
{
    private readonly SemaphoreSlim gate = new(1, 1);
    private readonly object stateGate = new();
    private string? token;
    private DateTimeOffset validUntil;
    public void Invalidate(string rejectedToken)
    {
        lock (stateGate)
            if (token == rejectedToken) validUntil = DateTimeOffset.MinValue;
    }
    public async Task<string> GetAsync(CancellationToken ct)
    {
        await gate.WaitAsync(ct);
        try
        {
            lock (stateGate)
                if (token is not null && validUntil > DateTimeOffset.UtcNow) return token;
            var o = options.Value;
            using var request = new HttpRequestMessage(HttpMethod.Post, o.TokenUrl);
            request.Headers.Add("__tenant", o.Tenant);
            request.Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "password", ["client_id"] = o.ClientId, ["client_secret"] = o.ClientSecret,
                ["username"] = o.Username, ["password"] = o.Password, ["scope"] = o.Scope
            });
            using var response = await http.SendAsync(request, ct);
            if (!response.IsSuccessStatusCode)
                throw new TouristPanelHttpException((int)response.StatusCode, "احراز هویت توریست‌پنل ناموفق بود", TouristPanelFailureKind.Authentication);
            TokenResponse? value;
            try { value = await response.Content.ReadFromJsonAsync<TokenResponse>(ct); }
            catch (Exception ex) when (ex is System.Text.Json.JsonException or NotSupportedException)
            { throw new TouristPanelHttpException(502, "پاسخ احراز هویت نامعتبر است", TouristPanelFailureKind.Authentication); }
            if (string.IsNullOrWhiteSpace(value?.AccessToken) || value.ExpiresIn <= 0
                || !string.Equals(value.TokenType, "Bearer", StringComparison.OrdinalIgnoreCase))
                throw new TouristPanelHttpException(502, "پاسخ احراز هویت نامعتبر است", TouristPanelFailureKind.Authentication);
            lock (stateGate)
            {
                token = value.AccessToken;
                validUntil = DateTimeOffset.UtcNow.AddSeconds(value.ExpiresIn - Math.Min(60, value.ExpiresIn / 10d));
                return token;
            }
        }
        finally { gate.Release(); }
    }
    private sealed record TokenResponse(
        [property: JsonPropertyName("access_token")] string AccessToken,
        [property: JsonPropertyName("expires_in")] int ExpiresIn,
        [property: JsonPropertyName("token_type")] string TokenType);
}

public enum TouristPanelFailureKind { Authentication, ProviderResponse, EmptyBody, Html, MalformedJson }

public sealed class TouristPanelHttpException(int statusCode, string message,
    TouristPanelFailureKind kind = TouristPanelFailureKind.ProviderResponse) : Exception(message)
{
    public int StatusCode { get; } = statusCode;
    public TouristPanelFailureKind Kind { get; } = kind;
}
