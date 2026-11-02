using System.Text.Json;
namespace Refahi.Modules.Entertainments.Application.Contracts.Discovery;
public static class EntertainmentPublicMetadata
{
    public static string? Project(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try {
            using var doc = JsonDocument.Parse(json);
            var allowed = new[] { "entertainments_order_id", "ProgramKey", "OfferKey", "PurchaseOptionKey" };
            return JsonSerializer.Serialize(doc.RootElement.EnumerateObject().Where(p => allowed.Contains(p.Name)).ToDictionary(p => p.Name, p => p.Value));
        } catch (JsonException) { return null; }
    }
}
