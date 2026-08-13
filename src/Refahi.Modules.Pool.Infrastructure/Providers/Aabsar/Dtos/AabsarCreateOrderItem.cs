using System.Text.Json.Serialization;

namespace Refahi.Modules.Pool.Infrastructure.Providers.Aabsar.Dtos;

public sealed class AabsarCreateOrderItem
{
    /// <summary>
    /// Important: the remote contract uses camelCase "showtimeId" here,
    /// unlike check-showtime which uses snake_case "showtime_id".
    /// </summary>
    [JsonPropertyName("showtimeId")]
    public string ShowtimeId { get; init; } = string.Empty;

    [JsonPropertyName("adult_quantity")]
    public int AdultQuantity { get; init; }

    [JsonPropertyName("child_quantity")]
    public int ChildQuantity { get; init; }

    /// <summary>
    /// Optional partner-price override for this line item.
    /// Omit/null to let Aabsar calculate the partner price.
    /// </summary>
    [JsonPropertyName("adult_price")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public long? AdultPrice { get; init; }

    /// <summary>
    /// Optional partner-price override for this line item.
    /// Omit/null to let Aabsar calculate the partner price.
    /// </summary>
    [JsonPropertyName("child_price")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public long? ChildPrice { get; init; }
}

