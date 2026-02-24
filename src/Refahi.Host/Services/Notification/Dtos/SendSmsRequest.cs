using System.Text.Json.Serialization;

namespace Refahi.Host.Services.Notification.Dtos;

/// <summary>
/// Request to send SMS message(s)
/// </summary>
public class SendSmsRequest
{
    /// <summary>
    /// Array of recipient phone numbers
    /// </summary>
    [JsonPropertyName("phoneNumbers")]
    public required string[] PhoneNumbers { get; init; }

    /// <summary>
    /// SMS text body (supports {{time}} placeholder)
    /// </summary>
    [JsonPropertyName("body")]
    public required string Body { get; init; }

    /// <summary>
    /// Sender number or name
    /// </summary>
    [JsonPropertyName("sender")]
    public string? Sender { get; init; }

    /// <summary>
    /// SMS gateway to use
    /// </summary>
    [JsonPropertyName("gateway")]
    public SmsGateway? Gateway { get; init; }
}
