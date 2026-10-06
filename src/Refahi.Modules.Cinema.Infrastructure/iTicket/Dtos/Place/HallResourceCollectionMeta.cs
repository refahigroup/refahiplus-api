#nullable enable

using System.Text.Json.Serialization;

namespace Refahi.Modules.Cinema.Infrastructure.iTicket;

public sealed class HallResourceCollectionMeta
{
    [JsonPropertyName("current_page")]
    public int CurrentPage { get; init; }

    [JsonPropertyName("from")]
    public int? From { get; init; }

    [JsonPropertyName("last_page")]
    public int LastPage { get; init; }

    [JsonPropertyName("path")]
    public string Path { get; init; }

    [JsonPropertyName("per_page")]
    public int PerPage { get; init; }

    [JsonPropertyName("to")]
    public int? To { get; init; }

    [JsonPropertyName("total")]
    public int Total { get; init; }

}
