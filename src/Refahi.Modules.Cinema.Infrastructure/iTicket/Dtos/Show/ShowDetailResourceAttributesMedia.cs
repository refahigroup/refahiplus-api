#nullable enable

using System.Text.Json.Serialization;

namespace Refahi.Modules.Cinema.Infrastructure.iTicket;

public sealed class ShowDetailResourceAttributesMedia
{
    [JsonPropertyName("poster")]
    public Dictionary<string,string> Poster { get; init; }

    [JsonPropertyName("banner")]
    public Dictionary<string,string> Banner { get; init; }

    [JsonPropertyName("trailer")]
    public string? Trailer { get; init; }

    [JsonPropertyName("gallery")]
    public List<Dictionary<string,string>> Gallery { get; init; }

}
