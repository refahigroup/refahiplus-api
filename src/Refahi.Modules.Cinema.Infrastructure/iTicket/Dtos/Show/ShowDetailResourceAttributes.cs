#nullable enable

using System.Text.Json;
using System.Text.Json.Serialization;

namespace Refahi.Modules.Cinema.Infrastructure.iTicket;

public sealed class ShowDetailResourceAttributes
{
    [JsonPropertyName("title")]
    public string Title { get; init; }

    [JsonPropertyName("slug")]
    public string? Slug { get; init; }

    [JsonPropertyName("summary")]
    public string? Summary { get; init; }

    [JsonPropertyName("description")]
    public string? Description { get; init; }

    [JsonPropertyName("age_group")]
    public string? AgeGroup { get; init; }

    [JsonPropertyName("film_grade")]
    public string? FilmGrade { get; init; }

    [JsonPropertyName("release_date")]
    public string? ReleaseDate { get; init; }

    [JsonPropertyName("duration_minutes")]
    public int? DurationMinutes { get; init; }

    [JsonPropertyName("display_priority")]
    public int DisplayPriority { get; init; }

    [JsonPropertyName("published_at")]
    public string? PublishedAt { get; init; }

    [JsonPropertyName("categories")]
    public List<ShowDetailResourceAttributesCategorie> Categories { get; init; }

    [JsonPropertyName("tags")]
    public List<ShowDetailResourceAttributesTag> Tags { get; init; }

    [JsonPropertyName("genres")]
    public List<ShowDetailResourceAttributesGenre> Genres { get; init; }

    [JsonPropertyName("screenings")]
    public List<ShowDetailResourceAttributesScreening> Screenings { get; init; }

    [JsonPropertyName("artists")]
    public List<JsonElement> Artists { get; init; }

    [JsonPropertyName("media")]
    public ShowDetailResourceAttributesMedia Media { get; init; }

}
