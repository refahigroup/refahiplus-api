using System.Text.Json.Serialization;

namespace Refahi.Modules.Pools.Infrastructure.Providers.Aabsar.Dtos;

public sealed class AabsarHealthResponse
{
    [JsonPropertyName("status")]
    public string? Status { get; init; }
}

