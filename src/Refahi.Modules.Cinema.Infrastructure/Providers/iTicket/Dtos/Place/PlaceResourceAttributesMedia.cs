using System.Text.Json.Serialization;

namespace Refahi.Modules.Cinema.Infrastructure.Providers.iTicket.Dtos.Place;

public sealed class PlaceResourceAttributesMedia
{
    [JsonPropertyName("logo")]
    public Dictionary<string,string> Logo { get; init; }

}
