using System.Text.Json.Serialization;

namespace Refahi.Host.Services.Notification.Dtos;

/// <summary>
/// Request to send email message(s)
/// </summary>
public class SendEmailRequest
{
    /// <summary>
    /// Array of recipient email addresses
    /// </summary>
    [JsonPropertyName("addresses")]
    public required string[] Addresses { get; init; }

    /// <summary>
    /// Email subject
    /// </summary>
    [JsonPropertyName("subject")]
    public required string Subject { get; init; }

    /// <summary>
    /// Email body content
    /// </summary>
    [JsonPropertyName("body")]
    public required string Body { get; init; }

    /// <summary>
    /// Whether body is HTML or plain text
    /// </summary>
    [JsonPropertyName("isHtml")]
    public required bool IsHtml { get; init; }
}
