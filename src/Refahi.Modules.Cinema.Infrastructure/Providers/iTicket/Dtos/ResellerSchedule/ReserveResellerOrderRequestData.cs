using System.Text.Json.Serialization;

namespace Refahi.Modules.Cinema.Infrastructure.Providers.iTicket.Dtos.ResellerSchedule;

public sealed class ReserveResellerOrderRequestData
{
    [JsonPropertyName("type")]
    public string Type { get; init; }

    [JsonPropertyName("attributes")]
    public ReserveResellerOrderRequestDataAttributes Attributes { get; init; }

}
