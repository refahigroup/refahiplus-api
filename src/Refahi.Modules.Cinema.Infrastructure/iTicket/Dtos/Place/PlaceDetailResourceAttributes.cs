#nullable enable

using System.Text.Json.Serialization;

namespace Refahi.Modules.Cinema.Infrastructure.iTicket;

public sealed class PlaceDetailResourceAttributes
{
    [JsonPropertyName("title")]
    public string Title { get; init; }

    [JsonPropertyName("slug")]
    public string? Slug { get; init; }

    [JsonPropertyName("description")]
    public string? Description { get; init; }

    [JsonPropertyName("display_priority")]
    public int DisplayPriority { get; init; }

    [JsonPropertyName("province_id")]
    public int? ProvinceId { get; init; }

    [JsonPropertyName("province_name")]
    public string? ProvinceName { get; init; }

    [JsonPropertyName("city_id")]
    public int? CityId { get; init; }

    [JsonPropertyName("city_name")]
    public string? CityName { get; init; }

    [JsonPropertyName("latitude")]
    public string? Latitude { get; init; }

    [JsonPropertyName("longitude")]
    public string? Longitude { get; init; }

    [JsonPropertyName("cinema_grade")]
    public string? CinemaGrade { get; init; }

    [JsonPropertyName("cinema_grade_label")]
    public string? CinemaGradeLabel { get; init; }

    [JsonPropertyName("rules")]
    public string? Rules { get; init; }

    [JsonPropertyName("venue")]
    public PlaceDetailResourceAttributesVenue Venue { get; init; }

    [JsonPropertyName("tags")]
    public List<PlaceDetailResourceAttributesTag> Tags { get; init; }

    [JsonPropertyName("show_categories")]
    public List<PlaceDetailResourceAttributesShowCategorie> ShowCategories { get; init; }

    [JsonPropertyName("amenities")]
    public List<PlaceDetailResourceAttributesAmenitie> Amenities { get; init; }

    [JsonPropertyName("contact")]
    public PlaceDetailResourceAttributesContact Contact { get; init; }

    [JsonPropertyName("media")]
    public PlaceDetailResourceAttributesMedia Media { get; init; }

}
