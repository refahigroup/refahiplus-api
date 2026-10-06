#nullable enable

using System.Text.Json.Serialization;

namespace Refahi.Modules.Cinema.Infrastructure.iTicket;

public sealed class SeatMapResourceAttributesBlock
{
    [JsonPropertyName("id")]
    public string Id { get; init; }

    [JsonPropertyName("name")]
    public string Name { get; init; }

    [JsonPropertyName("color")]
    public string Color { get; init; }

    [JsonPropertyName("sort_order")]
    public string SortOrder { get; init; }

    [JsonPropertyName("grid_row_count")]
    public string GridRowCount { get; init; }

    [JsonPropertyName("grid_column_count")]
    public string GridColumnCount { get; init; }

    [JsonPropertyName("row_labels")]
    public string RowLabels { get; init; }

    [JsonPropertyName("status")]
    public string Status { get; init; }

    [JsonPropertyName("status_label")]
    public string StatusLabel { get; init; }

    [JsonPropertyName("capacity")]
    public int Capacity { get; init; }

    [JsonPropertyName("available_count")]
    public int AvailableCount { get; init; }

    [JsonPropertyName("seats")]
    public List<string> Seats { get; init; }

}
