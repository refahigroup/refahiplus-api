using System.Text.Json.Serialization;

namespace Refahi.Modules.Cinema.Infrastructure.Providers.iTicket.Dtos.ResellerSchedule;

public sealed class ReserveResellerOrderRequest
{
    [JsonPropertyName("data")]
    public ReserveResellerOrderRequestData Data { get; init; }

}
