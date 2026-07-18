using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Refahi.Modules.Charge.Infrastructure.Providers.Eniac;

public static partial class ProviderPayloadSanitizer
{
    public const int MaxSnapshotLength = 16 * 1024;
    private static readonly HashSet<string> SensitiveKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "authorization", "password", "username", "token", "accessToken", "refreshToken",
        "pin", "pinCode", "pinChargeCode", "serial", "pinChargeSerial", "securePan",
        "hashedCardNumber", "providerRawCallback"
    };

    public static string SanitizeObject(object? value) =>
        SanitizeJson(value is null ? "{}" : JsonSerializer.Serialize(value));

    public static string SanitizeJson(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return "{}";

        try
        {
            var node = JsonNode.Parse(json);
            Redact(node);
            return LimitJson(node?.ToJsonString() ?? "{}");
        }
        catch (JsonException)
        {
            return LimitJson(JsonSerializer.Serialize(new { invalidJson = true, body = MaskMobiles(json) }));
        }
    }

    public static string SafeMessage(string? message) =>
        string.IsNullOrWhiteSpace(message) ? string.Empty : Limit(MaskMobiles(message), 2000);

    private static void Redact(JsonNode? node)
    {
        if (node is JsonObject obj)
        {
            foreach (var property in obj.ToList())
            {
                if (SensitiveKeys.Contains(property.Key))
                    obj[property.Key] = "***";
                else if (property.Value is JsonValue value && value.TryGetValue<string>(out var text))
                    obj[property.Key] = MaskMobiles(text);
                else
                    Redact(property.Value);
            }
        }
        else if (node is JsonArray array)
        {
            foreach (var item in array) Redact(item);
        }
    }

    private static string MaskMobiles(string value) => MobileRegex().Replace(value, m => $"{m.Value[..4]}***{m.Value[^4..]}");
    private static string Limit(string value, int max = MaxSnapshotLength) => value.Length <= max ? value : value[..max];

    private static string LimitJson(string json)
    {
        if (json.Length <= MaxSnapshotLength) return json;

        var bodyLength = Math.Min(json.Length, MaxSnapshotLength - 128);
        while (bodyLength > 0)
        {
            var result = JsonSerializer.Serialize(new { truncated = true, body = json[..bodyLength] });
            if (result.Length <= MaxSnapshotLength) return result;
            bodyLength -= Math.Max(64, result.Length - MaxSnapshotLength);
        }

        return "{\"truncated\":true}";
    }

    [GeneratedRegex(@"(?<!\d)09\d{9}(?!\d)", RegexOptions.CultureInvariant)]
    private static partial Regex MobileRegex();
}
