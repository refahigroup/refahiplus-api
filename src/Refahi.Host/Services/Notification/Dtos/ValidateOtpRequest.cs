using System.Text.Json.Serialization;

namespace Refahi.Host.Services.Notification.Dtos;

/// <summary>
/// Request to validate OTP
/// </summary>
public record ValidateOtpRequest
{
    /// <summary>
    /// Reference code from Generate response
    /// </summary>
    [JsonPropertyName("referenceCode")]
    public required string ReferenceCode { get; init; }

    /// <summary>
    /// OTP code received via SMS/Email
    /// </summary>
    [JsonPropertyName("code")]
    public required string Code { get; init; }
}
