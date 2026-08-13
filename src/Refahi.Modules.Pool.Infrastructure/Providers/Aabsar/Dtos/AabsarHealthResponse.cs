using System.Text.Json.Serialization;

namespace Refahi.Modules.Pool.Infrastructure.Providers.Aabsar.Dtos;

public sealed class AabsarHealthResponse
{
    [JsonPropertyName("status")]
    public string? Status { get; init; }
}

