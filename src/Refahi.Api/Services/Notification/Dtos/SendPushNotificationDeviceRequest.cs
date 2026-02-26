using System.Text.Json.Serialization;

namespace Refahi.Api.Services.Notification.Dtos;

/// <summary>
/// Push notification target device configuration
/// </summary>
public class SendPushNotificationDeviceRequest
{
    /// <summary>
    /// Device token
    /// </summary>
    [JsonPropertyName("address")]
    public required string Address { get; init; }

    /// <summary>
    /// Device type (Android or iOS)
    /// </summary>
    [JsonPropertyName("deviceType")]
    public required DeviceType DeviceType { get; init; }

    /// <summary>
    /// Target app name
    /// </summary>
    [JsonPropertyName("appName")]
    public AppName? AppName { get; init; }
}
