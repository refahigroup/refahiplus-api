namespace Refahi.Modules.Hotels.Domain.Aggregates.Catalogue;

public sealed class HotelProviderInventory
{
    private HotelProviderInventory() { }
    public string Provider { get; private set; } = "";
    public string CityId { get; private set; } = "";
    public string PayloadJson { get; private set; } = "{}";
    public DateTime RefreshedAtUtc { get; private set; }
    public static HotelProviderInventory Create(string provider, string cityId, string payload) =>
        new() { Provider = provider, CityId = cityId, PayloadJson = payload, RefreshedAtUtc = DateTime.UtcNow };
    public void Refresh(string payload) { PayloadJson = payload; RefreshedAtUtc = DateTime.UtcNow; }
}
