using System.Text.Json.Serialization;

namespace Refahi.Api.Services.Notification.Dtos;

/// <summary>
/// Request to send push notification(s)
/// </summary>
public class SendPushNotificationRequest
{
    /// <summary>
    /// Target devices
    /// </summary>
    [JsonPropertyName("addresses")]
    public required SendPushNotificationDeviceRequest[] Addresses { get; init; }

    /// <summary>
    /// Notification title
    /// </summary>
    [JsonPropertyName("subject")]
    public required string Subject { get; init; }

    /// <summary>
    /// Notification body text
    /// </summary>
    [JsonPropertyName("body")]
    public required string Body { get; init; }

    /// <summary>
    /// Deep link URL
    /// </summary>
    [JsonPropertyName("url")]
    public string? Url { get; init; }

    /// <summary>
    /// Custom JSON payload
    /// </summary>
    [JsonPropertyName("data")]
    public string? Data { get; init; }
}
