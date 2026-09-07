using System.Text.Json.Serialization;

namespace Refahi.Modules.Commerce.Infrastructure.Providers.Asbsar.Dtos;

public sealed class AabsarApiResponse<T>
{
    [JsonPropertyName("statusCode")]
    public int StatusCode { get; init; }

    [JsonPropertyName("message")]
    public string? Message { get; init; }

    [JsonPropertyName("data")]
    public T? Data { get; init; }
}

