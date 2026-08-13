using Refahi.Modules.Pool.Infrastructure.Providers.Aabsar.Abstraction;
using Refahi.Modules.Pool.Infrastructure.Providers.Aabsar.Dtos;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Refahi.Modules.Pool.Infrastructure.Providers.Aabsar;

public sealed class AabsarApiClient : IAabsarApiClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient _httpClient;

    public AabsarApiClient(HttpClient httpClient)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
    }

    public async Task<AabsarHealthResponse> HealthCheckAsync(CancellationToken cancellationToken = default)
    {
        // An empty relative URI keeps the full configured BaseAddress path.
        // Using "/" here would incorrectly reset the path to the host root.
        using var request = new HttpRequestMessage(HttpMethod.Get, string.Empty);
        using var response = await _httpClient.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);

        return await ReadResponseAsync<AabsarHealthResponse>(response, cancellationToken);
    }

    public async Task<AabsarApiResponse<IReadOnlyList<AabsarShowtimeDto>>> GetShowtimesAsync(
        CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "showtimes");
        using var response = await _httpClient.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);

        return await ReadResponseAsync<AabsarApiResponse<IReadOnlyList<AabsarShowtimeDto>>>(
            response,
            cancellationToken);
    }

    public async Task<AabsarApiResponse<AabsarShowtimeCapacityDto>> CheckShowtimeAsync(
        AabsarCheckShowtimeRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidateCheckShowtimeRequest(request);

        using var httpRequest = CreateJsonRequest(HttpMethod.Post, "check-showtime", request);
        using var response = await _httpClient.SendAsync(
            httpRequest,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);

        return await ReadResponseAsync<AabsarApiResponse<AabsarShowtimeCapacityDto>>(
            response,
            cancellationToken);
    }

    public async Task<AabsarApiResponse<AabsarCreateOrderResultDto>> CreateOrderAsync(
        AabsarCreateOrderRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidateCreateOrderRequest(request);

        using var httpRequest = CreateJsonRequest(HttpMethod.Post, "order-created", request);
        using var response = await _httpClient.SendAsync(
            httpRequest,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);

        return await ReadResponseAsync<AabsarApiResponse<AabsarCreateOrderResultDto>>(
            response,
            cancellationToken);
    }

    public async Task<AabsarApiResponse<JsonElement?>> CancelTicketsAsync(
        AabsarCancelTicketsRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidateCancelTicketsRequest(request);

        using var httpRequest = CreateJsonRequest(HttpMethod.Post, "cancel-tickets", request);
        using var response = await _httpClient.SendAsync(
            httpRequest,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);

        return await ReadResponseAsync<AabsarApiResponse<JsonElement?>>(response, cancellationToken);
    }

    private static HttpRequestMessage CreateJsonRequest<T>(HttpMethod method, string requestUri, T body)
    {
        return new HttpRequestMessage(method, requestUri)
        {
            Content = JsonContent.Create(body, options: JsonOptions)
        };
    }

    private static async Task<T> ReadResponseAsync<T>(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        var responseBody = response.Content is null
            ? string.Empty
            : await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw CreateApiException(response.StatusCode, response.ReasonPhrase, responseBody);
        }

        if (string.IsNullOrWhiteSpace(responseBody))
        {
            throw new AabsarApiException(
                message: $"Aabsar API returned an empty response body for HTTP {(int)response.StatusCode}.",
                statusCode: response.StatusCode,
                apiMessage: null,
                validationErrors: null,
                responseBody: responseBody);
        }

        try
        {
            var result = JsonSerializer.Deserialize<T>(responseBody, JsonOptions);
            if (result is null)
            {
                throw new JsonException("The JSON response deserialized to null.");
            }

            return result;
        }
        catch (JsonException ex)
        {
            throw new AabsarApiException(
                message: $"Aabsar API returned an unexpected JSON response for HTTP {(int)response.StatusCode}.",
                statusCode: response.StatusCode,
                apiMessage: null,
                validationErrors: null,
                responseBody: responseBody,
                innerException: ex);
        }
    }

    private static AabsarApiException CreateApiException(
        HttpStatusCode statusCode,
        string? reasonPhrase,
        string? responseBody)
    {
        string? apiMessage = null;
        JsonElement? validationErrors = null;

        if (!string.IsNullOrWhiteSpace(responseBody))
        {
            try
            {
                using var document = JsonDocument.Parse(responseBody);
                var root = document.RootElement;

                if (root.ValueKind == JsonValueKind.Object)
                {
                    if (root.TryGetProperty("message", out var messageElement) &&
                        messageElement.ValueKind == JsonValueKind.String)
                    {
                        apiMessage = messageElement.GetString();
                    }

                    if (root.TryGetProperty("errors", out var errorsElement))
                    {
                        validationErrors = errorsElement.Clone();
                    }
                }
            }
            catch (JsonException)
            {
                // Preserve the raw response body below even when an error response is not valid JSON.
            }
        }

        var message = $"Aabsar API request failed with HTTP {(int)statusCode} ({reasonPhrase ?? statusCode.ToString()})";
        if (!string.IsNullOrWhiteSpace(apiMessage))
        {
            message += $": {apiMessage}";
        }

        return new AabsarApiException(
            message: message,
            statusCode: statusCode,
            apiMessage: apiMessage,
            validationErrors: validationErrors,
            responseBody: responseBody);
    }

    private static void ValidateCheckShowtimeRequest(AabsarCheckShowtimeRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.ShowtimeId))
        {
            throw new ArgumentException("showtime_id is required.", nameof(request));
        }

        if (request.ShowtimeId.Length > 255)
        {
            throw new ArgumentOutOfRangeException(
                nameof(request),
                "showtime_id must not exceed 255 characters.");
        }
    }

    private static void ValidateCreateOrderRequest(AabsarCreateOrderRequest request)
    {
        if (request.Items is null)
        {
            throw new ArgumentException("items is required.", nameof(request));
        }

        if (request.User is null)
        {
            throw new ArgumentException("user is required.", nameof(request));
        }

        foreach (var item in request.Items)
        {
            if (item is null)
            {
                throw new ArgumentException("items cannot contain null entries.", nameof(request));
            }

            if (!IsUlid(item.ShowtimeId))
            {
                throw new ArgumentException(
                    $"showtimeId '{item.ShowtimeId}' must be a valid 26-character ULID.",
                    nameof(request));
            }

            ValidateRange(item.AdultQuantity, 0, 100, "adult_quantity", nameof(request));
            ValidateRange(item.ChildQuantity, 0, 100, "child_quantity", nameof(request));

            if (item.AdultPrice is < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(request), "adult_price must be greater than or equal to 0.");
            }

            if (item.ChildPrice is < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(request), "child_price must be greater than or equal to 0.");
            }
        }

        var phone = request.User.Phone;
        if (string.IsNullOrEmpty(phone) ||
            phone.Length != 10 ||
            phone[0] == '0' ||
            phone.Any(c => c is < '0' or > '9'))
        {
            throw new ArgumentException(
                "phone must contain exactly 10 ASCII digits without a leading 0, e.g. 9123456789.",
                nameof(request));
        }

        var fullNameLength = request.User.FullName?.Length ?? 0;
        if (fullNameLength is < 3 or > 255)
        {
            throw new ArgumentException("full_name must contain between 3 and 255 characters.", nameof(request));
        }
    }

    private static void ValidateCancelTicketsRequest(AabsarCancelTicketsRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.OrderCode))
        {
            throw new ArgumentException("order_code is required.", nameof(request));
        }

        ValidateRange(request.Adult, 0, 100, "adult", nameof(request));
        ValidateRange(request.Child, 0, 100, "child", nameof(request));
    }

    private static void ValidateRange(int value, int min, int max, string fieldName, string parameterName)
    {
        if (value < min || value > max)
        {
            throw new ArgumentOutOfRangeException(
                parameterName,
                $"{fieldName} must be between {min} and {max}.");
        }
    }

    private static bool IsUlid(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length != 26)
        {
            return false;
        }

        // Crockford Base32 alphabet used by ULID (case-insensitive).
        const string alphabet = "0123456789ABCDEFGHJKMNPQRSTVWXYZ";

        foreach (var character in value)
        {
            if (!alphabet.Contains(char.ToUpperInvariant(character)))
            {
                return false;
            }
        }

        // The first Base32 character of a 128-bit ULID can only be 0-7.
        return value[0] is >= '0' and <= '7';
    }
}