using System.Text.Json.Serialization;

namespace Refahi.Modules.Cinema.Infrastructure.Providers.iTicket.Dtos.Place;

public sealed class PlaceDetailResourceAttributesVenue
{
    [JsonPropertyName("floor_area_sqm")]
    public decimal? FloorAreaSqm { get; init; }

    [JsonPropertyName("lobby_area_sqm")]
    public decimal? LobbyAreaSqm { get; init; }

    [JsonPropertyName("staff_count")]
    public int? StaffCount { get; init; }

    [JsonPropertyName("parking_spaces")]
    public int? ParkingSpaces { get; init; }

    [JsonPropertyName("founded_at")]
    public string? FoundedAt { get; init; }

    [JsonPropertyName("is_renovated")]
    public bool IsRenovated { get; init; }

    [JsonPropertyName("renovated_at")]
    public string? RenovatedAt { get; init; }

}
