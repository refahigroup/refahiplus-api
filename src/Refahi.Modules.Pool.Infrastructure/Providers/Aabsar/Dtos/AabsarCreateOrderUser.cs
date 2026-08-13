using System.Text.Json.Serialization;

namespace Refahi.Modules.Pool.Infrastructure.Providers.Aabsar.Dtos;

public sealed class AabsarCreateOrderUser
{
    /// <summary>
    /// Exactly 10 ASCII digits without leading zero, e.g. "9123456789".
    /// The API document's table calls this an integer, while every JSON example
    /// sends it as a string. This implementation follows the actual JSON examples.
    /// </summary>
    [JsonPropertyName("phone")]
    public string Phone { get; init; } = string.Empty;

    [JsonPropertyName("full_name")]
    public string FullName { get; init; } = string.Empty;
}

