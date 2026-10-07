using System.Text.Json.Serialization;

namespace Refahi.Modules.Cinema.Infrastructure.Providers.iTicket.Dtos.ShowCategory;

public sealed class ShowCategoryResourceCollection
{
    [JsonPropertyName("data")]
    public List<ShowCategoryResource> Data { get; init; }

    [JsonPropertyName("links")]
    public ShowCategoryResourceCollectionLinks Links { get; init; }

    [JsonPropertyName("meta")]
    public ShowCategoryResourceCollectionMeta Meta { get; init; }

}
