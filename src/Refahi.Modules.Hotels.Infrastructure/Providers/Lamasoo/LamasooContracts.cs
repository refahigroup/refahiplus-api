using System.Text.Json.Serialization;
namespace Refahi.Modules.Hotels.Infrastructure.Providers.Lamasoo;

internal sealed class LamasooInventory { public List<LamasooHotel> Hotels { get; set; } = []; }
internal sealed class LamasooCities { public List<LamasooCity> Cities { get; set; } = []; }
internal sealed class LamasooCity { public string Id { get; set; } = ""; public string Name { get; set; } = ""; public string? Code { get; set; } }
internal sealed class LamasooHotel
{
    public string Id { get; set; } = ""; public string Name { get; set; } = ""; public string City { get; set; } = "";
    public string? Address { get; set; }
    public string? Desc { get; set; }
    public int Star { get; set; }
    public List<LamasooImage> Images { get; set; } = []; public List<LamasooRoom> RoomTypes { get; set; } = [];
    public List<LamasooRate> RoomRates { get; set; } = []; public List<LamasooPlan> RatePlans { get; set; } = [];
    public List<LamasooChildPolicy> ChildPolicies { get; set; } = [];
}
internal sealed class LamasooImage { public string? Url { get; set; } }
internal sealed class LamasooRoom
{
    public string Id { get; set; } = ""; public string Title { get; set; } = ""; public int Capacity { get; set; }
    public int ExtraCapacity { get; set; }
    public int MaxChildrenWithoutBed { get; set; }
    public int RoomsRemaining { get; set; }
    public List<LamasooDay> RoomsRemainingPerDay { get; set; } = [];
}
internal sealed class LamasooDay
{
    public DateOnly Date { get; set; }
    public int Count { get; set; }
    public int Dedicated { get; set; }
    [JsonPropertyName("CTA")] public bool ClosedToArrival { get; set; }
    [JsonPropertyName("CTD")] public bool ClosedToDeparture { get; set; }
    [JsonPropertyName("minLOS")] public int MinLos { get; set; }
    [JsonPropertyName("maxLOS")] public int MaxLos { get; set; }
    [JsonPropertyName("stopSell")] public bool StopSell { get; set; }
}
internal sealed class LamasooChildPolicy { public decimal From { get; set; } public decimal To { get; set; } public string Type { get; set; } = ""; public bool RequiresBed { get; set; } }
internal sealed class LamasooRate
{
    public string RoomTypeId { get; set; } = ""; public string RatePlanId { get; set; } = ""; public string Currency { get; set; } = "";
    public List<LamasooPrice> Prices { get; set; } = []; public LamasooPrice Price { get; set; } = new();
}
internal sealed class LamasooPrice
{
    public DateOnly Date { get; set; }
    public long DisplayPrice { get; set; }
    public long PriceToPay { get; set; }
    public long ExtraGuestPrice { get; set; }
    public long ExtraGuestPriceToPay { get; set; }
    public List<LamasooChildPrice> ChildPricing { get; set; } = [];
}
internal sealed class LamasooChildPrice { public string Type { get; set; } = ""; public string? Name { get; set; } public long PriceToPay { get; set; } }
internal sealed class LamasooPlan { public string Id { get; set; } = ""; public string Title { get; set; } = ""; public string MealType { get; set; } = "RO"; public string? AgencyProviderId { get; set; } }
internal sealed class LamasooAvailability { public string Currency { get; set; } = ""; public List<LamasooHotel> Hotels { get; set; } = []; public LamasooWage Wage { get; set; } = new(); public List<System.Text.Json.JsonElement> Errors { get; set; } = []; }
internal sealed class LamasooWage { public string Type { get; set; } = ""; public decimal Amount { get; set; } public long? Max { get; set; } }
internal sealed class LamasooSubmitResponse
{
    public string Status { get; set; } = ""; public string ReferenceId { get; set; } = "";
    public List<System.Text.Json.JsonElement> Problems { get; set; } = []; public LamasooPayment PaymentDetails { get; set; } = new();
}
internal sealed class LamasooPayment { public long PriceToPay { get; set; } public long HotelShare { get; set; } public long LamasooShare { get; set; } }
internal sealed class LamasooConfirmResponse { public string Status { get; set; } = ""; public string ReservationId { get; set; } = ""; }
