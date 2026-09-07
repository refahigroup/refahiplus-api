using System.Text.Json.Serialization;

namespace Refahi.Modules.Commerce.Infrastructure.Providers.Asbsar.Dtos;

public sealed class AabsarCheckShowtimeRequest
{
    [JsonPropertyName("showtime_id")]
    public string ShowtimeId { get; init; } = string.Empty;
}

