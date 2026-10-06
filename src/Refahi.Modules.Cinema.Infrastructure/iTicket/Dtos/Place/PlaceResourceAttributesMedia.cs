#nullable enable

using System.Text.Json.Serialization;

namespace Refahi.Modules.Cinema.Infrastructure.iTicket;

public sealed class PlaceResourceAttributesMedia
{
    [JsonPropertyName("logo")]
    public Dictionary<string,string> Logo { get; init; }

}
