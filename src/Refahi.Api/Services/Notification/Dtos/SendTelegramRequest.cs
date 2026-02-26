using System.Text.Json.Serialization;

namespace Refahi.Api.Services.Notification.Dtos;

/// <summary>
/// Request to send Telegram message
/// </summary>
public class SendTelegramRequest
{
    /// <summary>
    /// Telegram chat ID or channel ID
    /// </summary>
    [JsonPropertyName("chatId")]
    public required string ChatId { get; init; }

    /// <summary>
    /// Message text
    /// </summary>
    [JsonPropertyName("body")]
    public string? Body { get; init; }

    /// <summary>
    /// File name for attachments
    /// </summary>
    [JsonPropertyName("fileName")]
    public string? FileName { get; init; }

    /// <summary>
    /// File binary data
    /// </summary>
    [JsonPropertyName("fileData")]
    public byte[]? FileData { get; init; }
}
