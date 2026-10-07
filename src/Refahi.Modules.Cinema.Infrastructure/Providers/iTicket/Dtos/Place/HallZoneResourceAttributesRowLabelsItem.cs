using System.Text.Json.Serialization;

namespace Refahi.Modules.Cinema.Infrastructure.Providers.iTicket.Dtos.Place;

public sealed class HallZoneResourceAttributesRowLabelsItem
{
    [JsonPropertyName("grid_row")]
    public int GridRow { get; init; }

    [JsonPropertyName("label")]
    public string Label { get; init; }

    [JsonPropertyName("sort_order")]
    public int SortOrder { get; init; }
}