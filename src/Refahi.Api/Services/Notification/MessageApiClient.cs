using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Refahi.Api.Services.Notification.Dtos;

namespace Refahi.Api.Services.Notification;

/// <summary>
/// HTTP client for Message API (SMS, Email, Telegram, Push Notifications)
/// </summary>
public class MessageApiClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<MessageApiClient> _logger;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters =
        {
            new JsonStringEnumConverter(JsonNamingPolicy.CamelCase)
        }
    };

    public MessageApiClient(HttpClient httpClient, ILogger<MessageApiClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    /// <summary>
    /// Send multi-channel message (SMS, Email, Telegram, Push Notification)
    /// </summary>
    /// <param name="request">Message request with one or more channels configured</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <exception cref="MessageApiException">Thrown when API call fails</exception>
    public async Task SendMessageAsync(
        SendMessageRequest request, 
        CancellationToken cancellationToken = default)
    {
        try
        {
            var channels = GetConfiguredChannels(request);
            _logger.LogDebug(
                "Sending message via {Channels}. MessageId={MessageId}, DueTime={DueTime}",
                string.Join(", ", channels), 
                request.Id, 
                request.DueTime);

            var response = await _httpClient.PostAsJsonAsync(
                "/V1/Message", 
                request, 
                JsonOptions,
                cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation(
                    "Message sent successfully via {Channels}. MessageId={MessageId}",
                    string.Join(", ", channels), 
                    request.Id ?? Guid.NewGuid());

                return;
            }

            await HandleErrorResponseAsync(response, cancellationToken);
            throw new MessageApiException("Failed to send message", response.StatusCode);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP request failed while sending message");
            throw new MessageApiException("Failed to communicate with Message service", ex);
        }
        catch (TaskCanceledException ex)
        {
            _logger.LogWarning(ex, "Message send request timed out");
            throw new MessageApiException("Message service request timed out", ex);
        }
    }

    private static string[] GetConfiguredChannels(SendMessageRequest request)
    {
        var channels = new System.Collections.Generic.List<string>();
        
        if (request.Sms is not null) channels.Add("SMS");
        if (request.Email is not null) channels.Add("Email");
        if (request.Telegram is not null) channels.Add("Telegram");
        if (request.PushNotification is not null) channels.Add("Push");
        
        return channels.ToArray();
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
                    "Message API error. StatusCode={StatusCode}, Message={Message}",
                    response.StatusCode,
                    errorMessage);

                throw new MessageApiException(errorMessage, response.StatusCode);
            }
        }
        catch (JsonException)
        {
            // If error response cannot be parsed, log raw content
            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogError(
                "Message API returned error. StatusCode={StatusCode}, Content={Content}",
                response.StatusCode,
                content);
        }
    }
}

/// <summary>
/// Exception thrown when Message API calls fail
/// </summary>
public class MessageApiException : Exception
{
    public HttpStatusCode? StatusCode { get; }

    public MessageApiException(string message) : base(message)
    {
    }

    public MessageApiException(string message, HttpStatusCode statusCode) : base(message)
    {
        StatusCode = statusCode;
    }

    public MessageApiException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
