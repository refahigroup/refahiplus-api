using Refahi.Modules.Hotels.Application.Contracts.Catalogue;
namespace Refahi.Modules.Hotels.Infrastructure.Providers.Lamasoo;

internal static class LamasooHotelMapper
{
    internal static ProviderHotel Hotel(LamasooHotel item) => new(item.Id, item.City, item.Name, item.Address ?? "", item.Star,
        item.Images.Select(x => x.Url).Where(x => Uri.TryCreate(x, UriKind.Absolute, out var uri) && uri.Scheme == "https").Cast<string>().ToArray(), item.Desc ?? "");
    internal static IReadOnlyList<ProviderHotelOffer> Map(LamasooAvailability availability, IReadOnlyList<LamasooHotel> inventory, HotelTravel travel)
    {
        if (availability.Currency != "IRR") throw new InvalidOperationException("واحد پول پاسخ هتل معتبر نیست.");
        var result = new List<ProviderHotelOffer>();
        foreach (var hotel in availability.Hotels)
        {
            var presentation = inventory.FirstOrDefault(x => x.Id == hotel.Id); if (presentation is null) continue;
            if (inventory.Count(x => x.Id == hotel.Id) != 1 || string.IsNullOrWhiteSpace(presentation.Id)
                || string.IsNullOrWhiteSpace(presentation.City) || presentation.Star is < 0 or > 5) continue;
            foreach (var rate in hotel.RoomRates)
            {
                var room = hotel.RoomTypes.FirstOrDefault(x => x.Id == rate.RoomTypeId);
                var plan = hotel.RatePlans.FirstOrDefault(x => x.Id == rate.RatePlanId);
                if (hotel.RoomTypes.Count(x => x.Id == rate.RoomTypeId) != 1 || hotel.RatePlans.Count(x => x.Id == rate.RatePlanId) != 1
                    || hotel.RoomRates.Count(x => x.RoomTypeId == rate.RoomTypeId && x.RatePlanId == rate.RatePlanId) != 1) continue;
                if (room is null || plan is null || rate.Currency != "IRR" || room.Capacity <= 0 || room.ExtraCapacity < 0 || room.MaxChildrenWithoutBed < 0 || room.RoomsRemaining < travel.Rooms.Count) continue;
                if (string.IsNullOrWhiteSpace(room.Id) || string.IsNullOrWhiteSpace(plan.Id) || plan.MealType is not ("RO" or "BB" or "HB" or "FB" or "AI" or "UAI")) continue;
                var nights = travel.CheckOut.DayNumber - travel.CheckIn.DayNumber;
                if (nights <= 0 || rate.Prices.Count != nights || rate.Prices.Select(x => x.Date).Distinct().Count() != nights) continue;
                if (Enumerable.Range(0, nights).Any(i => room.RoomsRemainingPerDay.Count(x => x.Date == travel.CheckIn.AddDays(i)) != 1)) continue;
                if (Enumerable.Range(0, nights).Any(i => !rate.Prices.Any(x => x.Date == travel.CheckIn.AddDays(i) && x.DisplayPrice > 0 && x.PriceToPay > 0)
                    || !room.RoomsRemainingPerDay.Any(x => x.Date == travel.CheckIn.AddDays(i) && x.Count >= travel.Rooms.Count && !x.StopSell))) continue;
                var arrival = room.RoomsRemainingPerDay.FirstOrDefault(x => x.Date == travel.CheckIn);
                if (arrival is null || arrival.ClosedToArrival || arrival.MinLos > nights || (arrival.MaxLos > 0 && arrival.MaxLos < nights)
                    || room.RoomsRemainingPerDay.Any(x => x.Date == travel.CheckOut && x.ClosedToDeparture)) continue;
                long display = 0, supply = 0; var valid = true;
                foreach (var party in travel.Rooms)
                {
                    var policies = party.ChildrenAges.Select(age => hotel.ChildPolicies.FirstOrDefault(x => age >= x.From && age <= x.To)).ToArray();
                    if (policies.Any(x => x is null) || party.ChildrenAges.Any(age => hotel.ChildPolicies.Count(x => age >= x.From && age <= x.To) != 1)) { valid = false; break; }
                    var childrenWithBed = policies.Count(x => x!.RequiresBed);
                    var withoutBed = policies.Length - childrenWithBed;
                    if (withoutBed > room.MaxChildrenWithoutBed || party.Adults + childrenWithBed > room.Capacity + room.ExtraCapacity) { valid = false; break; }
                    var extraAdults = Math.Max(0, party.Adults - room.Capacity);
                    foreach (var day in rate.Prices)
                    {
                        if (day.ExtraGuestPrice < 0 || day.ExtraGuestPriceToPay < 0) { valid = false; break; }
                        display = checked(display + day.DisplayPrice + extraAdults * day.ExtraGuestPrice);
                        supply = checked(supply + day.PriceToPay + extraAdults * day.ExtraGuestPriceToPay);
                        foreach (var policy in policies)
                        {
                            var prices = day.ChildPricing.Where(x => (x.Type.Length > 0 ? x.Type : x.Name) == policy!.Type).ToArray();
                            if (prices.Length != 1 || prices[0].PriceToPay < 0) { valid = false; break; }
                            // The public contract has no child display price; preserve the quoted child charge without markup.
                            display = checked(display + prices[0].PriceToPay); supply = checked(supply + prices[0].PriceToPay);
                        }
                    }
                }
                if (!valid || display <= 0 || supply <= 0) continue;
                var wage = availability.Wage;
                if (wage.Amount < 0 || wage.Max < 0 || wage.Type is not ("percent" or "fixed")) continue;
                var fee = wage.Type switch { "percent" => checked((long)decimal.Ceiling(supply * wage.Amount / 100m)), "fixed" => checked((long)decimal.Ceiling(wage.Amount)), _ => 0 };
                if (wage.Max is > 0) fee = Math.Min(fee, wage.Max.Value);
                if (fee < 0) continue;
                result.Add(new(Hotel(presentation), room.Id, room.Title, plan.Id, Board(plan.MealType), room.Capacity,
                    display, checked(supply + fee), fee, plan.AgencyProviderId, System.Text.Json.JsonSerializer.Serialize(new { rate, plan, room, hotel.ChildPolicies, wage }, LamasooApiClient.Json)));
            }
        }
        return result;
    }
    private static string Board(string meal) => meal switch { "BB" => "BedBreakfast", "HB" => "HalfBoard", "FB" => "FullBoard", "AI" => "AllInclusive", "UAI" => "UltraAllInclusive", _ => "RoomOnly" };
}
