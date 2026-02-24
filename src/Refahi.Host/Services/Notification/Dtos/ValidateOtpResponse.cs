using System.Text.Json.Serialization;

namespace Refahi.Host.Services.Notification.Dtos;

/// <summary>
/// Response from validate OTP request
/// </summary>
public record ValidateOtpResponse
{
    /// <summary>
    /// Whether OTP is valid
    /// </summary>
    [JsonPropertyName("isValid")]
    public required bool IsValid { get; init; }

    /// <summary>
    /// Remaining validation attempts
    /// </summary>
    [JsonPropertyName("attemptsRemaining")]
    public required int AttemptsRemaining { get; init; }

    /// <summary>
    /// Result message
    /// </summary>
    [JsonPropertyName("message")]
    public string? Message { get; init; }

    /// <summary>
    /// The destination (phone/email) this OTP was sent to (only present when IsValid = true)
    /// </summary>
    [JsonPropertyName("destination")]
    public string? Destination { get; init; }

    /// <summary>
    /// The type of destination: "mobile" or "email" (only present when IsValid = true)
    /// </summary>
    [JsonPropertyName("destinationType")]
    public string? DestinationType { get; init; }
}
