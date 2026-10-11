using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Refahi.Modules.Hotels.Application.Contracts.Catalogue;
namespace Refahi.Modules.Hotels.Infrastructure.Providers.Lamasoo;

public sealed class LamasooHotelProvider(LamasooApiClient api, IOptions<LamasooOptions> options, IMemoryCache cache,
    Catalogue.HotelInventoryStore inventoryStore) : IHotelCatalogueProvider
{
    public string Name => "Lamasoo";
    // Public documentation does not provide inquiry/cancellation. Purchase stays disabled until those adapters exist.
    public HotelProviderCapabilities Capabilities => new(options.Value.SearchEnabled, false, false, false, false);
    public async Task<IReadOnlyList<ProviderCity>> CitiesAsync(CancellationToken ct)
    {
        var cities = await api.SendAsync<LamasooCities>(HttpMethod.Get, "booking/cities", null, false, ct);
        return cities.Cities.Select(x =>
        {
            var country = options.Value.CityCountries.GetValueOrDefault(x.Id) ?? "";
            var reviewed = country == "IR" && options.Value.CityCodes.TryGetValue(x.Id, out var code) && code == x.Code;
            return new ProviderCity(x.Id, x.Name, x.Code, country,
                reviewed && options.Value.CityMappings.TryGetValue(x.Id, out var legacy) ? legacy : null);
        }).ToArray();
    }
    private async Task<IReadOnlyList<LamasooHotel>> InventoryAsync(string city, CancellationToken ct)
    {
        var key = $"hotels:lamasoo:inventory:{city}";
        if (cache.TryGetValue<IReadOnlyList<LamasooHotel>>(key, out var value) && value is not null) return value;
        var payload = await inventoryStore.GetAsync(Name, city, async () =>
            JsonSerializer.Serialize(await api.SendAsync<LamasooInventory>(HttpMethod.Get, "booking/hotel_inventory?city_id=" + Uri.EscapeDataString(city), null, false, ct), LamasooApiClient.Json), ct);
        var inventory = JsonSerializer.Deserialize<LamasooInventory>(payload, LamasooApiClient.Json) ?? throw new JsonException();
        cache.Set(key, (IReadOnlyList<LamasooHotel>)inventory.Hotels, TimeSpan.FromDays(1)); return inventory.Hotels;
    }
    public async Task<IReadOnlyList<ProviderHotelOffer>> SearchAsync(string cityId, HotelTravel travel, CancellationToken ct)
    {
        var inventory = await InventoryAsync(cityId, ct);
        var availability = await api.SendAsync<LamasooAvailability>(HttpMethod.Post, "booking/booking_availability_v2",
            new
            {
                lang = "fa_IR",
                start_date = travel.CheckIn.ToString("yyyy-MM-dd"),
                end_date = travel.CheckOut.ToString("yyyy-MM-dd"),
                currency = "IRR",
                user_country = "IR",
                city_id = cityId,
                hotel_ids = Array.Empty<string>(),
                show_constraints = true,
                skip_price_validation = false
            }, false, ct);
        if (availability.Errors.Count > 0) throw new InvalidOperationException("پاسخ جست‌وجوی هتل کامل نیست.");
        return LamasooHotelMapper.Map(availability, inventory, travel);
    }
    public async Task<ProviderHotel> DetailsAsync(string id, string cityId, CancellationToken ct) =>
        LamasooHotelMapper.Hotel((await InventoryAsync(cityId, ct)).SingleOrDefault(x => x.Id == id)
            ?? throw new InvalidOperationException("هتل یافت نشد."));
    public async Task<HotelSubmitResult> SubmitAsync(HotelSubmitRequest request, CancellationToken ct)
    {
        var customer = request.Guests.First(x => x.Type == "Adult"); var started = DateTime.UtcNow;
        var response = await api.SendAsync<LamasooSubmitResponse>(HttpMethod.Post, "booking/async/booking_submit", new
        {
            checkin_date = request.Travel.CheckIn.ToString("yyyy-MM-dd"),
            checkout_date = request.Travel.CheckOut.ToString("yyyy-MM-dd"),
            hotel_id = request.Offer.Hotel.Id,
            reference_id = request.ReferenceId,
            rate_plan_id = request.Offer.RatePlanId,
            agency_provider_id = request.Offer.AgencyProviderId,
            customer = new { first_name = customer.FirstName, last_name = customer.LastName, national_code = customer.NationalCode, phone_number = request.Phone, country = customer.Country },
            rooms = request.Travel.Rooms.Select((party, index) => new
            {
                type_id = request.Offer.RoomId,
                count = 1,
                party = new { adults = party.Adults, children = party.ChildrenAges },
                guests = request.Guests.Where(x => x.RoomIndex == index)
                    .Select(x => new { first_name = x.FirstName, last_name = x.LastName, national_code = x.NationalCode, country = x.Country })
            })
        }, true, ct);
        if (response.Status != "Success" || response.Problems is null || response.PaymentDetails is null || response.Problems.Count > 0 || response.ReferenceId != request.ReferenceId
            || response.PaymentDetails.PriceToPay <= 0 || response.PaymentDetails.LamasooShare < 0
            || response.PaymentDetails.HotelShare < 0 || response.PaymentDetails.LamasooShare > response.PaymentDetails.PriceToPay
            || response.PaymentDetails.HotelShare != response.PaymentDetails.PriceToPay - response.PaymentDetails.LamasooShare)
            throw new HotelExternalOutcomeUnknownException("پاسخ رزرو نیاز به بررسی دارد.");
        return new(response.ReferenceId, response.PaymentDetails.PriceToPay, response.PaymentDetails.LamasooShare,
            started.AddMinutes(15), JsonSerializer.Serialize(response, LamasooApiClient.Json));
    }
    public async Task<HotelConfirmResult> ConfirmAsync(string referenceId, CancellationToken ct)
    {
        var result = await api.SendAsync<LamasooConfirmResponse>(HttpMethod.Post, "booking/async/booking_confirm", new { reference_id = referenceId }, true, ct);
        if (result.Status != "hotel-confirmed" || string.IsNullOrWhiteSpace(result.ReservationId))
            throw new HotelExternalOutcomeUnknownException("تأیید نهایی رزرو نیاز به بررسی دارد.");
        return new(result.ReservationId, "Confirmed");
    }
}
