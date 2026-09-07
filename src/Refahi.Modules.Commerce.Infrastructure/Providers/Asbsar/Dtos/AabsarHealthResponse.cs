using System.Text.Json.Serialization;

namespace Refahi.Modules.Commerce.Infrastructure.Providers.Asbsar.Dtos;

public sealed class AabsarHealthResponse
{
    [JsonPropertyName("status")]
    public string? Status { get; init; }
}

