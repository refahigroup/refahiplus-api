using System.Text.Json.Serialization;

namespace Refahi.Host.Services.Notification.Dtos;

/// <summary>
/// API error response structure
/// </summary>
public record ApiErrorResponse
{
    /// <summary>
    /// HTTP status code
    /// </summary>
    [JsonPropertyName("statusCode")]
    public int StatusCode { get; init; }

    /// <summary>
    /// Error message
    /// </summary>
    [JsonPropertyName("message")]
    public string? Message { get; init; }

    /// <summary>
    /// Validation errors
    /// </summary>
    [JsonPropertyName("errors")]
    public string[]? Errors { get; init; }
}
