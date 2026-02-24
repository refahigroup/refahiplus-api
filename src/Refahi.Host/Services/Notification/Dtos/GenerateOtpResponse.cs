using System;
using System.Text.Json.Serialization;

namespace Refahi.Host.Services.Notification.Dtos;

/// <summary>
/// Response from generate OTP request
/// </summary>
public record GenerateOtpResponse
{
    /// <summary>
    /// Reference code for validation
    /// </summary>
    [JsonPropertyName("referenceCode")]
    public required string ReferenceCode { get; init; }

    /// <summary>
    /// OTP expiration time
    /// </summary>
    [JsonPropertyName("expiresAt")]
    public required DateTime ExpiresAt { get; init; }

    /// <summary>
    /// Success message
    /// </summary>
    [JsonPropertyName("message")]
    public string? Message { get; init; }
}
