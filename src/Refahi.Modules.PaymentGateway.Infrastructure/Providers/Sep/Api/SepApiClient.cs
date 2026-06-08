using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Refahi.Modules.PaymentGateway.Infrastructure.Providers.Sep.Config;
using Refahi.Modules.PaymentGateway.Infrastructure.Providers.Sep.Contract;
using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Refahi.Modules.PaymentGateway.Infrastructure.Providers.Sep.Api;

/// <summary>
/// HTTP client wrapper for SEP (Saman Electronic Payment) API.
/// Injected by HttpClientFactory with Polly resilience policies.
/// </summary>
public class SepApiClient
{
    private readonly HttpClient _http;
    private readonly SepOptions _options;
    private readonly ILogger<SepApiClient> _logger;

    public SepApiClient(
        HttpClient http,
        IOptions<SepOptions> options,
        ILogger<SepApiClient> logger)
    {
        _http = http;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<SepTokenResponse> RequestTokenAsync(SepTokenRequest request, CancellationToken ct)
    {
        _logger.LogInformation("SEP: Requesting token for ResNum={ResNum} Amount={Amount}",
            request.ResNum, request.Amount);

        var response = await _http.PostAsJsonAsync(_options.TokenUrl, request, ct);

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(ct);
            _logger.LogError("SEP token request failed. Status={Status} Body={Body}",
                response.StatusCode, body);
            throw new HttpRequestException(
                $"SEP token request failed with status {response.StatusCode}. Body: {body}");
        }

        var result = await response.Content.ReadFromJsonAsync<SepTokenResponse>(ct)
            ?? throw new InvalidOperationException("SEP returned an empty token response.");

        _logger.LogInformation("SEP: Token response Status={Status}", result.Status);
        return result;
    }

    public async Task<SepVerifyResponse> VerifyTransactionAsync(SepVerifyRequest request, CancellationToken ct)
    {
        _logger.LogInformation("SEP: Verifying transaction RefNum={RefNum}", request.RefNum);

        var response = await _http.PostAsJsonAsync(_options.VerifyUrl, request, ct);

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(ct);
            _logger.LogError("SEP verify request failed. Status={Status} Body={Body}",
                response.StatusCode, body);
            throw new HttpRequestException(
                $"SEP verify request failed with status {response.StatusCode}. Body: {body}");
        }

        var result = await response.Content.ReadFromJsonAsync<SepVerifyResponse>(ct)
            ?? throw new InvalidOperationException("SEP returned an empty verify response.");

        _logger.LogInformation("SEP: Verify response ResultCode={ResultCode} Success={Success}",
            result.ResultCode, result.Success);
        return result;
    }

    public async Task<SepReverseResponse> ReverseTransactionAsync(SepReverseRequest request, CancellationToken ct)
    {
        _logger.LogInformation("SEP: Reversing transaction RefNum={RefNum}", request.RefNum);

        var response = await _http.PostAsJsonAsync(_options.ReverseUrl, request, ct);

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(ct);
            _logger.LogError("SEP reverse request failed. Status={Status} Body={Body}",
                response.StatusCode, body);
            throw new HttpRequestException(
                $"SEP reverse request failed with status {response.StatusCode}. Body: {body}");
        }

        var result = await response.Content.ReadFromJsonAsync<SepReverseResponse>(ct)
            ?? throw new InvalidOperationException("SEP returned an empty reverse response.");

        _logger.LogInformation("SEP: Reverse response ResultCode={ResultCode} Success={Success}",
            result.ResultCode, result.Success);
        return result;
    }
}
