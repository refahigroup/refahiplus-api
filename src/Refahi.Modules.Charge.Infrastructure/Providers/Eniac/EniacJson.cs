using System.Text.Json;
namespace Refahi.Modules.Charge.Infrastructure.Providers.Eniac;

internal static class EniacJson
{
    public static JsonElement Data(JsonElement root) =>
        root.TryGetProperty("data", out var value) ? value : default;

    public static string? String(JsonElement e, params string[] names)
    {
        foreach (var n in names)
        {
            if (e.ValueKind == JsonValueKind.Object && e.TryGetProperty(n, out var v))
                return v.ValueKind == JsonValueKind.String
                    ? v.GetString()
                    : (v.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
                        ? null
                        : v.GetRawText();
        }

        return null;
    }

    public static long Long(JsonElement e, params string[] names)
        => long.TryParse(String(e, names), out var value)
            ? value
            : 0;

    public static int Int(JsonElement e, params string[] names)
        => int.TryParse(String(e, names), out var value)
            ? value
            : 0;

    public static int? NullableInt(JsonElement e, params string[] names)
        => int.TryParse(String(e, names), out var value)
            ? value
            : null;

    public static bool Bool(JsonElement e, params string[] names)
    {
        foreach (var n in names)
        {
            if (e.ValueKind == JsonValueKind.Object && e.TryGetProperty(n, out var v))
                return v.ValueKind == JsonValueKind.True || (v.ValueKind == JsonValueKind.String && bool.TryParse(v.GetString(), out var b) && b);
        }

        return false;
    }
}
