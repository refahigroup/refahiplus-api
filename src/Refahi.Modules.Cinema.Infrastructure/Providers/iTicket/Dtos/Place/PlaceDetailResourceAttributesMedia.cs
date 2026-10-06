using System.Text.Json.Serialization;

namespace Refahi.Modules.Cinema.Infrastructure.Providers.iTicket.Dtos.Place;

public sealed class PlaceDetailResourceAttributesMedia
{
    [JsonPropertyName("logo")]
    public Dictionary<string,string> Logo { get; init; }

    [JsonPropertyName("primary_media")]
    public Dictionary<string,string> PrimaryMedia { get; init; }

    [JsonPropertyName("intro_video")]
    public string? IntroVideo { get; init; }

    [JsonPropertyName("gallery")]
    public List<Dictionary<string,string>> Gallery { get; init; }

}
