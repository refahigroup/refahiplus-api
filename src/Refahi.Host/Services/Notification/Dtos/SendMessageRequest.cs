using System;
using System.Text.Json.Serialization;

namespace Refahi.Host.Services.Notification.Dtos;

/// <summary>
/// Main request to send multi-channel message
/// </summary>
public class SendMessageRequest
{
    /// <summary>
    /// Unique message identifier (auto-generated if not provided)
    /// </summary>
    [JsonPropertyName("id")]
    public Guid? Id { get; init; }

    /// <summary>
    /// Scheduled send time (null for immediate)
    /// </summary>
    [JsonPropertyName("dueTime")]
    public DateTime? DueTime { get; init; }

    /// <summary>
    /// URL to validate message before sending
    /// </summary>
    [JsonPropertyName("validatorUrl")]
    public string? ValidatorUrl { get; init; }

    /// <summary>
    /// Tags for categorization and bulk operations
    /// </summary>
    [JsonPropertyName("tags")]
    public string[]? Tags { get; init; }

    /// <summary>
    /// SMS configuration
    /// </summary>
    [JsonPropertyName("sms")]
    public SendSmsRequest? Sms { get; init; }

    /// <summary>
    /// Email configuration
    /// </summary>
    [JsonPropertyName("email")]
    public SendEmailRequest? Email { get; init; }

    /// <summary>
    /// Telegram configuration
    /// </summary>
    [JsonPropertyName("telegram")]
    public SendTelegramRequest? Telegram { get; init; }

    /// <summary>
    /// Push notification configuration
    /// </summary>
    [JsonPropertyName("pushNotification")]
    public SendPushNotificationRequest? PushNotification { get; init; }
}
