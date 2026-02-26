using System.Text.Json.Serialization;

namespace Refahi.Api.Services.Notification.Dtos;

/// <summary>
/// Request to generate OTP
/// </summary>
public record GenerateOtpRequest
{
    /// <summary>
    /// Phone number or email address
    /// </summary>
    [JsonPropertyName("destination")]
    public required string Destination { get; init; }

    /// <summary>
    /// Type: "sms" or "email"
    /// </summary>
    [JsonPropertyName("type")]
    public required string Type { get; init; }

    /// <summary>
    /// Purpose like "login", "signup", "reset-password"
    /// </summary>
    [JsonPropertyName("purpose")]
    public string? Purpose { get; init; }

    /// <summary>
    /// Time to live in minutes
    /// </summary>
    [JsonPropertyName("ttlMinutes")]
    public int? TtlMinutes { get; init; }

    /// <summary>
    /// OTP code length
    /// </summary>
    [JsonPropertyName("length")]
    public int? Length { get; init; }
}
