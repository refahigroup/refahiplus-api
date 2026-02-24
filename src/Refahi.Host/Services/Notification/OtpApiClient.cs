using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Refahi.Host.Services.Notification.Dtos;

namespace Refahi.Host.Services.Notification;

/// <summary>
/// HTTP client for OTP API
/// </summary>
public class OtpApiClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<OtpApiClient> _logger;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public OtpApiClient(HttpClient httpClient, ILogger<OtpApiClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    /// <summary>
    /// Generate OTP and send to destination
    /// </summary>
    public async Task<GenerateOtpResponse> GenerateAsync(
        GenerateOtpRequest request, 
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("Sending OTP generate request to {Destination}", request.Destination);

            var response = await _httpClient.PostAsJsonAsync(
                "/V1/Otp/Generate", 
                request, 
                JsonOptions,
                cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<GenerateOtpResponse>(
                    JsonOptions, 
                    cancellationToken);

                if (result is null)
                {
                    throw new OtpApiException("Failed to deserialize generate OTP response", HttpStatusCode.InternalServerError);
                }

                _logger.LogInformation(
                    "OTP generated successfully. ReferenceCode={ReferenceCode}, ExpiresAt={ExpiresAt}",
                    result.ReferenceCode, 
                    result.ExpiresAt);

                return result;
            }

            await HandleErrorResponseAsync(response, cancellationToken);
            throw new OtpApiException("Failed to generate OTP", response.StatusCode);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP request failed while generating OTP");
            throw new OtpApiException("Failed to communicate with OTP service", ex);
        }
        catch (TaskCanceledException ex)
        {
            _logger.LogWarning(ex, "OTP generate request timed out");
            throw new OtpApiException("OTP service request timed out", ex);
        }
    }

    /// <summary>
    /// Validate OTP code
    /// </summary>
    public async Task<ValidateOtpResponse> ValidateAsync(
        ValidateOtpRequest request, 
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("Sending OTP validate request for ReferenceCode={ReferenceCode}", request.ReferenceCode);

            var response = await _httpClient.PostAsJsonAsync(
                "/V1/Otp/Validate", 
                request, 
                JsonOptions,
                cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<ValidateOtpResponse>(
                    JsonOptions, 
                    cancellationToken);

                if (result is null)
                {
                    throw new OtpApiException("Failed to deserialize validate OTP response", HttpStatusCode.InternalServerError);
                }

                _logger.LogInformation(
                    "OTP validation completed. ReferenceCode={ReferenceCode}, IsValid={IsValid}, AttemptsRemaining={AttemptsRemaining}",
                    request.ReferenceCode, 
                    result.IsValid, 
                    result.AttemptsRemaining);

                return result;
            }

            // For validation, 400 with structured response is expected
            if (response.StatusCode == HttpStatusCode.BadRequest)
            {
                var result = await response.Content.ReadFromJsonAsync<ValidateOtpResponse>(
                    JsonOptions, 
                    cancellationToken);

                if (result is not null)
                {
                    _logger.LogWarning(
                        "OTP validation failed. ReferenceCode={ReferenceCode}, Message={Message}",
                        request.ReferenceCode, 
                        result.Message);

                    return result;
                }
            }

            await HandleErrorResponseAsync(response, cancellationToken);
            throw new OtpApiException("Failed to validate OTP", response.StatusCode);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP request failed while validating OTP");
            throw new OtpApiException("Failed to communicate with OTP service", ex);
        }
        catch (TaskCanceledException ex)
        {
            _logger.LogWarning(ex, "OTP validate request timed out");
            throw new OtpApiException("OTP service request timed out", ex);
        }
    }

    private async Task HandleErrorResponseAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        try
        {
            var errorResponse = await response.Content.ReadFromJsonAsync<ApiErrorResponse>(
                JsonOptions, 
                cancellationToken);

            if (errorResponse is not null)
            {
                var errorMessage = errorResponse.Message ?? "Unknown error";
                if (errorResponse.Errors?.Length > 0)
                {
                    errorMessage = $"{errorMessage}: {string.Join(", ", errorResponse.Errors)}";
                }

                _logger.LogError(
                    "OTP API error. StatusCode={StatusCode}, Message={Message}",
                    response.StatusCode,
                    errorMessage);

                throw new OtpApiException(errorMessage, response.StatusCode);
            }
        }
        catch (JsonException)
        {
            // If error response cannot be parsed, log raw content
            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogError(
                "OTP API returned error. StatusCode={StatusCode}, Content={Content}",
                response.StatusCode,
                content);
        }
    }
}

/// <summary>
/// Exception thrown when OTP API calls fail
/// </summary>
public class OtpApiException : Exception
{
    public HttpStatusCode? StatusCode { get; }

    public OtpApiException(string message) : base(message)
    {
    }

    public OtpApiException(string message, HttpStatusCode statusCode) : base(message)
    {
        StatusCode = statusCode;
    }

    public OtpApiException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
