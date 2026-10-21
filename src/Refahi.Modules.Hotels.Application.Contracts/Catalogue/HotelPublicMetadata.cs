using System.Text.Json.Nodes;
namespace Refahi.Modules.Hotels.Application.Contracts.Catalogue;
public static class HotelPublicMetadata
{
    public static string? WithoutExternalImages(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return json;
        try { var node = JsonNode.Parse(json); Clean(node); return node?.ToJsonString(); }
        catch (System.Text.Json.JsonException) { return "{}"; }
    }
    private static void Clean(JsonNode? node)
    {
        if (node is JsonObject obj)
            foreach (var pair in obj.ToArray())
            {
                if (pair.Key.Contains("image", StringComparison.OrdinalIgnoreCase) || pair.Key.Contains("photo", StringComparison.OrdinalIgnoreCase)
                    || pair.Key.Contains("thumbnail", StringComparison.OrdinalIgnoreCase) || pair.Key.Contains("gallery", StringComparison.OrdinalIgnoreCase)) obj.Remove(pair.Key);
                else if (pair.Value is JsonValue value && value.TryGetValue<string>(out var text) && (text.TrimStart().StartsWith("{") || text.TrimStart().StartsWith("[")))
                    obj[pair.Key] = WithoutExternalImages(text);
                else Clean(pair.Value);
            }
        else if (node is JsonArray array) foreach (var child in array) Clean(child);
    }
}
