#nullable enable

using System.Text.Json.Serialization;

namespace Refahi.Modules.Cinema.Infrastructure.iTicket;

public sealed class ShowResourceAttributes
{
    [JsonPropertyName("title")]
    public string Title { get; init; }

    [JsonPropertyName("slug")]
    public string? Slug { get; init; }

    [JsonPropertyName("summary")]
    public string? Summary { get; init; }

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
    public List<ShowResourceAttributesCategorie> Categories { get; init; }

    [JsonPropertyName("tags")]
    public List<ShowResourceAttributesTag> Tags { get; init; }

    [JsonPropertyName("genres")]
    public List<ShowResourceAttributesGenre> Genres { get; init; }

    [JsonPropertyName("media")]
    public ShowResourceAttributesMedia Media { get; init; }

}
