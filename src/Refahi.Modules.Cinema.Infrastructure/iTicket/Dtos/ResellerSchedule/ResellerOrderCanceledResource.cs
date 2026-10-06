#nullable enable

using System.Text.Json.Serialization;

namespace Refahi.Modules.Cinema.Infrastructure.iTicket;

public sealed class ResellerOrderCanceledResource
{
    [JsonPropertyName("type")]
    public string Type { get; init; }

    [JsonPropertyName("id")]
    public string Id { get; init; }

    [JsonPropertyName("attributes")]
    public ResellerOrderCanceledResourceAttributes Attributes { get; init; }

}
