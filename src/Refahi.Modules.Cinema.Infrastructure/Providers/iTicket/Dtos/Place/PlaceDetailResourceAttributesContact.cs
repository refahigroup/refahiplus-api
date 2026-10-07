using System.Text.Json;
using System.Text.Json.Serialization;

namespace Refahi.Modules.Cinema.Infrastructure.Providers.iTicket.Dtos.Place;

public sealed class PlaceDetailResourceAttributesContact
{
    [JsonPropertyName("address")]
    public JsonElement Address { get; init; }

    [JsonPropertyName("phones")]
    public JsonElement Phones { get; init; }

    [JsonPropertyName("social")]
    public JsonElement Social { get; init; }

    [JsonPropertyName("directions")]
    public JsonElement Directions { get; init; }

}
