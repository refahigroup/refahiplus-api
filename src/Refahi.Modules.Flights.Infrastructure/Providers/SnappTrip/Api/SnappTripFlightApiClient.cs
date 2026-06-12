using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Refahi.Modules.Flights.Infrastructure.Providers.SnappTrip.Config;
using Refahi.Modules.Flights.Infrastructure.Providers.SnappTrip.Contract;
using Refahi.Modules.Flights.Infrastructure.Providers.SnappTrip.Logging;

namespace Refahi.Modules.Flights.Infrastructure.Providers.SnappTrip.Api;

internal sealed class SnappTripFlightApiClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _httpClient;
    private readonly ILogger<SnappTripFlightApiClient> _logger;
    private readonly SnappTripFlightOptions _options;

    public SnappTripFlightApiClient(
        HttpClient httpClient,
        ILogger<SnappTripFlightApiClient> logger,
        IOptions<SnappTripFlightOptions> options)
    {
        _httpClient = httpClient;
        _logger = logger;
        _options = options.Value;
    }

    public Task<SnappTripFlightApiResult<SnappTripSearchResponse>> SearchAsync(
        SnappTripSearchRequest request,
        CancellationToken cancellationToken)
    {
        return PostAsync<SnappTripSearchResponse>("search", request, cancellationToken);
    }

    public Task<SnappTripFlightApiResult<SnappTripBookResponse>> BookAsync(
        SnappTripBookRequest request,
        CancellationToken cancellationToken)
    {
        return PostAsync<SnappTripBookResponse>("book", request, cancellationToken);
    }

    public Task<SnappTripFlightApiResult<SnappTripIssueResponse>> IssueAsync(
        SnappTripIssueRequest request,
        CancellationToken cancellationToken)
    {
        return PostAsync<SnappTripIssueResponse>("issue", request, cancellationToken);
    }

    public Task<SnappTripFlightApiResult<SnappTripInquiryResponse>> InquiryAsync(
        string bookId,
        CancellationToken cancellationToken)
    {
        return GetAsync<SnappTripInquiryResponse>(
            $"inquiry/{Uri.EscapeDataString(bookId)}",
            cancellationToken);
    }

    public Task<SnappTripFlightApiResult<SnappTripPenaltyResponse>> QuoteCancellationAsync(
        SnappTripPenaltyRequest request,
        CancellationToken cancellationToken)
    {
        return PostAsync<SnappTripPenaltyResponse>("cancellations/penalty", request, cancellationToken);
    }

    public Task<SnappTripFlightApiResult<SnappTripCancelResponse>> SubmitCancellationAsync(
        SnappTripCancelRequest request,
        CancellationToken cancellationToken)
    {
        return PostAsync<SnappTripCancelResponse>("cancellations/submit", request, cancellationToken);
    }

    private async Task<SnappTripFlightApiResult<T>> GetAsync<T>(
        string path,
        CancellationToken cancellationToken)
    {
        var url = BuildUrl(path);

        _logger.LogInformation("SnappTrip Flight GET {Url}", SnappTripFlightLogMasker.MaskText(url));

        using var httpRequest = CreateRequest(HttpMethod.Get, url);
        using var response = await _httpClient.SendAsync(httpRequest, cancellationToken);
        return await ReadResponseAsync<T>(response, url, cancellationToken);
    }

    private async Task<SnappTripFlightApiResult<T>> PostAsync<T>(
        string path,
        object payload,
        CancellationToken cancellationToken)
    {
        var url = BuildUrl(path);
        var json = JsonSerializer.Serialize(payload, JsonOptions);

        _logger.LogInformation(
            "SnappTrip Flight POST {Url} Payload={Payload}",
            url,
            SnappTripFlightLogMasker.MaskText(json));

        using var content = new StringContent(json, Encoding.UTF8);
        content.Headers.ContentType = new MediaTypeHeaderValue("application/json");

        using var httpRequest = CreateRequest(HttpMethod.Post, url);
        httpRequest.Content = content;

        using var response = await _httpClient.SendAsync(httpRequest, cancellationToken);

        return await ReadResponseAsync<T>(response, url, cancellationToken);
    }

    private HttpRequestMessage CreateRequest(HttpMethod method, string url)
    {
        var request = new HttpRequestMessage(method, url);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        if (!_httpClient.DefaultRequestHeaders.Contains("api-key") &&
            !string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            request.Headers.TryAddWithoutValidation("api-key", _options.ApiKey);
        }

        return request;
    }

    private async Task<SnappTripFlightApiResult<T>> ReadResponseAsync<T>(
        HttpResponseMessage response,
        string url,
        CancellationToken cancellationToken)
    {
        var raw = await response.Content.ReadAsStringAsync(cancellationToken);
        var maskedRaw = SnappTripFlightLogMasker.MaskText(raw);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError(
                "SnappTrip Flight error calling {Url}. Status={StatusCode}, Body={Body}",
                url,
                (int)response.StatusCode,
                maskedRaw);

            throw new InvalidOperationException(
                $"SnappTrip Flight error calling {url}. Status={(int)response.StatusCode}.");
        }

        _logger.LogInformation(
            "SnappTrip Flight response {Url}. Status={StatusCode}, Body={Body}",
            url,
            (int)response.StatusCode,
            maskedRaw);

        var result = JsonSerializer.Deserialize<T>(raw, JsonOptions);
        if (result is null)
            throw new InvalidOperationException($"SnappTrip Flight {url} returned an empty response.");

        return new SnappTripFlightApiResult<T>(result, maskedRaw);
    }

    private string BuildUrl(string path)
    {
        return string.Join(
            '/',
            _options.BaseUrl.TrimEnd('/'),
            _options.ApiBasePath.Trim('/'),
            path.Trim('/'));
    }
}

internal sealed record SnappTripFlightApiResult<T>(T Data, string? MaskedRawPayload);
