using System.Text.Json.Serialization;

namespace Refahi.Modules.Pool.Infrastructure.Providers.Aabsar.Dtos;

public sealed class AabsarShowtimeCapacityDto
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;

    [JsonPropertyName("adult_price")]
    public long AdultPrice { get; init; }

    [JsonPropertyName("child_price")]
    public long ChildPrice { get; init; }

    [JsonPropertyName("adult_old_price")]
    public long? AdultOldPrice { get; init; }

    [JsonPropertyName("child_old_price")]
    public long? ChildOldPrice { get; init; }

    [JsonPropertyName("gender")]
    public string? Gender { get; init; }

    [JsonPropertyName("capacity")]
    public int Capacity { get; init; }

    [JsonPropertyName("status")]
    public string? Status { get; init; }
}

