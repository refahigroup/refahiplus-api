using System.Text.Json.Serialization;

namespace Refahi.Modules.Commerce.Infrastructure.Providers.Asbsar.Dtos;

public sealed class AabsarCreateOrderResultDto
{
    [JsonPropertyName("order_code")]
    public string OrderCode { get; init; } = string.Empty;

    [JsonPropertyName("total_price")]
    public long TotalPrice { get; init; }

    [JsonPropertyName("tickets")]
    public IReadOnlyList<AabsarTicketDto> Tickets { get; init; } = Array.Empty<AabsarTicketDto>();
}

