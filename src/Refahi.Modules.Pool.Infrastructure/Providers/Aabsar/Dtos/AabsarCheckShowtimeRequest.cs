using System.Text.Json.Serialization;

namespace Refahi.Modules.Pool.Infrastructure.Providers.Aabsar.Dtos;

public sealed class AabsarCheckShowtimeRequest
{
    [JsonPropertyName("showtime_id")]
    public string ShowtimeId { get; init; } = string.Empty;
}

