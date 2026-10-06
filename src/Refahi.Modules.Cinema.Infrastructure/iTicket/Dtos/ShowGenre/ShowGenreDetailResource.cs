#nullable enable

using System.Text.Json.Serialization;

namespace Refahi.Modules.Cinema.Infrastructure.iTicket;

public sealed class ShowGenreDetailResource
{
    [JsonPropertyName("type")]
    public string Type { get; init; }

    [JsonPropertyName("id")]
    public string Id { get; init; }

    [JsonPropertyName("attributes")]
    public ShowGenreDetailResourceAttributes Attributes { get; init; }

}
