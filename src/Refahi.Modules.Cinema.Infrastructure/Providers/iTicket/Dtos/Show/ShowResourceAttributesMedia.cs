using System.Text.Json.Serialization;

namespace Refahi.Modules.Cinema.Infrastructure.Providers.iTicket.Dtos.Show;

public sealed class ShowResourceAttributesMedia
{
    [JsonPropertyName("poster")]
    public Dictionary<string,string> Poster { get; init; }

}
