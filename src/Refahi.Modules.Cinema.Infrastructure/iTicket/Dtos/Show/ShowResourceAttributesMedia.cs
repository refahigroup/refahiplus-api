#nullable enable

using System.Text.Json.Serialization;

namespace Refahi.Modules.Cinema.Infrastructure.iTicket;

public sealed class ShowResourceAttributesMedia
{
    [JsonPropertyName("poster")]
    public Dictionary<string,string> Poster { get; init; }

}
