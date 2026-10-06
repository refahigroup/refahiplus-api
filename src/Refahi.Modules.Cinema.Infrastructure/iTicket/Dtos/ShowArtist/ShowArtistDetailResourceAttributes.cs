#nullable enable

using System.Text.Json.Serialization;

namespace Refahi.Modules.Cinema.Infrastructure.iTicket;

public sealed class ShowArtistDetailResourceAttributes
{
    [JsonPropertyName("name")]
    public string Name { get; init; }

    [JsonPropertyName("first_name")]
    public string? FirstName { get; init; }

    [JsonPropertyName("last_name")]
    public string? LastName { get; init; }

    [JsonPropertyName("slug")]
    public string? Slug { get; init; }

    [JsonPropertyName("bio")]
    public string? Bio { get; init; }

    [JsonPropertyName("biography_short")]
    public string? BiographyShort { get; init; }

    [JsonPropertyName("birth_date")]
    public string? BirthDate { get; init; }

    [JsonPropertyName("death_date")]
    public string? DeathDate { get; init; }

    [JsonPropertyName("gender")]
    public string? Gender { get; init; }

    [JsonPropertyName("gender_label")]
    public string? GenderLabel { get; init; }

    [JsonPropertyName("nationality")]
    public string? Nationality { get; init; }

    [JsonPropertyName("nationality_label")]
    public string? NationalityLabel { get; init; }

    [JsonPropertyName("birth_place")]
    public string? BirthPlace { get; init; }

    [JsonPropertyName("portrait")]
    public Dictionary<string,string> Portrait { get; init; }

    [JsonPropertyName("website_url")]
    public string? WebsiteUrl { get; init; }

}
