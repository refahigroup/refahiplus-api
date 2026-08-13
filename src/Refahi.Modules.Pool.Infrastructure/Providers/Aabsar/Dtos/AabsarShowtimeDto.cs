using System.Text.Json;
using System.Text.Json.Serialization;

namespace Refahi.Modules.Pool.Infrastructure.Providers.Aabsar.Dtos;

// ============================================================================
// Showtime DTOs
// ============================================================================

public sealed class AabsarShowtimeDto
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;

    [JsonPropertyName("event_id")]
    public string EventId { get; init; } = string.Empty;

    /// <summary>
    /// Unix timestamp in milliseconds.
    /// </summary>
    [JsonPropertyName("time")]
    public long Time { get; init; }

    [JsonPropertyName("title")]
    public string? Title { get; init; }

    [JsonPropertyName("show_time")]
    public string? ShowTime { get; init; }

    [JsonPropertyName("part_time")]
    public bool PartTime { get; init; }

    /// <summary>
    /// The source document only demonstrates null and does not define a concrete type,
    /// so JsonElement is used to avoid inventing a schema.
    /// </summary>
    [JsonPropertyName("announcement")]
    public JsonElement? Announcement { get; init; }

    [JsonPropertyName("adult_price")]
    public long AdultPrice { get; init; }

    [JsonPropertyName("child_price")]
    public long ChildPrice { get; init; }

    [JsonPropertyName("adult_old_price")]
    public long? AdultOldPrice { get; init; }

    [JsonPropertyName("child_old_price")]
    public long? ChildOldPrice { get; init; }

    /// <summary>
    /// Documented values: "male" or "female".
    /// Kept as string because the remote API contract is string-based.
    /// </summary>
    [JsonPropertyName("gender")]
    public string? Gender { get; init; }

    [JsonPropertyName("capacity")]
    public int Capacity { get; init; }

    [JsonPropertyName("event_title")]
    public string? EventTitle { get; init; }

    [JsonPropertyName("vendor_name")]
    public string? VendorName { get; init; }
}

