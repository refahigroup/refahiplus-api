using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Refahi.Modules.Hotels.Infrastructure.Providers.SnappTrip.Contract;

// GIS is optional: a malformed location must not discard an otherwise valid hotel inventory.
public sealed class SnappTripLocationConverter : JsonConverter<SnappTripHotelDetailLocation>
{
    public override SnappTripHotelDetailLocation Read(ref Utf8JsonReader reader, Type type, JsonSerializerOptions options)
    {
        using var value = JsonDocument.ParseValue(ref reader);
        return new() { lat = Coordinate(value.RootElement, "lat"), lon = Coordinate(value.RootElement, "lon") };
    }
    private static decimal? Coordinate(JsonElement element, string name)
    {
        if(element.ValueKind != JsonValueKind.Object || !element.TryGetProperty(name,out var value)) return null;
        if(value.ValueKind == JsonValueKind.Number && value.TryGetDecimal(out var number)) return number;
        return value.ValueKind == JsonValueKind.String && decimal.TryParse(value.GetString(),NumberStyles.Float,CultureInfo.InvariantCulture,out number) ? number : null;
    }
    public override void Write(Utf8JsonWriter writer, SnappTripHotelDetailLocation value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        if(value.lat is {} lat) writer.WriteNumber("lat",lat);
        if(value.lon is {} lon) writer.WriteNumber("lon",lon);
        writer.WriteEndObject();
    }
}
