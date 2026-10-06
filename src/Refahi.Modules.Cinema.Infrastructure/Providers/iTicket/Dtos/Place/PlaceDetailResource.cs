using System.Text.Json.Serialization;

namespace Refahi.Modules.Cinema.Infrastructure.Providers.iTicket.Dtos.Place;

public sealed class PlaceDetailResource
{
    [JsonPropertyName("type")]
    public string Type { get; init; }

    [JsonPropertyName("id")]
    public string Id { get; init; }

    [JsonPropertyName("attributes")]
    public PlaceDetailResourceAttributes Attributes { get; init; }

}
