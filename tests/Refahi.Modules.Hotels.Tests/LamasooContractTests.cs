using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Refahi.Modules.Hotels.Application.Contracts.Catalogue;
using Refahi.Modules.Hotels.Application.Features.Catalogue;
using Refahi.Modules.Hotels.Domain.Aggregates.Catalogue;
using Refahi.Modules.Hotels.Infrastructure.Providers.Lamasoo;
using Xunit;

namespace Refahi.Modules.Hotels.Tests;

public sealed class LamasooContractTests
{
    // Simplified published availability fixture; deliberately different daily and aggregate amounts.
    private const string Fixture = """
    {"currency":"IRR","wage":{"type":"percent","amount":1,"max":1500000},"hotels":[{
      "id":"hotel-native","name":"Hotel","room_types":[{"id":"room-native","title":"Room","capacity":2,
      "extra_capacity":1,"max_children_without_bed":1,"rooms_remaining":4,"rooms_remaining_per_day":[
      {"date":"2030-01-01","count":4,"CTA":false,"minLOS":1,"maxLOS":3,"stopSell":false},
      {"date":"2030-01-02","count":4,"stopSell":false}]}],
      "room_rates":[{"room_type_id":"room-native","rate_plan_id":"rate-native","currency":"IRR","prices":[
      {"date":"2030-01-01","display_price":1000,"price_to_pay":900,"extra_guest_price":100,"extra_guest_price_to_pay":80,
       "child_pricing":[{"type":"INFANT","price_to_pay":0},{"type":"CHILD","price_to_pay":200}]},
      {"date":"2030-01-02","display_price":2000,"price_to_pay":1800,"extra_guest_price":100,"extra_guest_price_to_pay":80,
       "child_pricing":[{"type":"INFANT","price_to_pay":0},{"type":"CHILD","price_to_pay":300}]}],
      "price":{"display_price":999999,"price_to_pay":888888}}],
      "rate_plans":[{"id":"rate-native","meal_type":"BB","agency_provider_id":"agency-native"}],
      "child_policies":[{"from":0,"to":1.99,"type":"INFANT","requires_bed":false},
      {"from":2,"to":5.99,"type":"CHILD","requires_bed":true}]}]}
    """;
    private static LamasooAvailability Availability() => JsonSerializer.Deserialize<LamasooAvailability>(Fixture, LamasooApiClient.Json)!;
    private static HotelTravel Travel(params HotelParty[] rooms) => new(new(2030, 1, 1), new(2030, 1, 3), rooms);
    private static IReadOnlyList<LamasooHotel> Inventory => [new() { Id = "hotel-native", Name = "Hotel", City = "city-native", Images = [new() { Url = "https://example.org/hotel.jpg" }] }];

    [Fact]
    public async Task NullableAggregateExtraGuestPrices_DoNotPreventNightlyAvailabilityPricing()
    {
        // Matches the live Tehran response: aggregate extra-guest fields are null, nightly amounts are numeric.
        var payload = Fixture.Replace("\"price\":{\"display_price\":999999,\"price_to_pay\":888888}",
            "\"price\":{\"display_price\":999999,\"price_to_pay\":888888,\"extra_guest_price\":null,\"extra_guest_final_price\":null,\"extra_guest_price_to_pay\":null}");
        var handler = new FakeHttpHandler(_ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        { Content = new StringContent(payload, System.Text.Encoding.UTF8, "application/json") }));
        var api = new LamasooApiClient(new HttpClient(handler) { BaseAddress = new("https://example.org/") }, NullLogger<LamasooApiClient>.Instance);
        var availability = await api.SendAsync<LamasooAvailability>(HttpMethod.Post, "booking/booking_availability_v2", new { }, false, default);
        var offer = Assert.Single(LamasooHotelMapper.Map(availability, Inventory, Travel(new HotelParty(2, []))));
        Assert.Equal(3000, offer.DisplayPriceMinor);
        Assert.Equal(2727, offer.SupplyPriceMinor);
        Assert.Equal(27, offer.FeeMinor);
        Assert.Equal(1, handler.Calls);
    }

    [Theory]
    [InlineData("display_price", "1000")]
    [InlineData("price_to_pay", "900")]
    [InlineData("extra_guest_price", "100")]
    [InlineData("extra_guest_price_to_pay", "80")]
    public void NullNightlyAmounts_AreNotSilentlyConvertedToZero(string field, string value)
    {
        var payload = Fixture.Replace($"\"{field}\":{value}", $"\"{field}\":null");
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<LamasooAvailability>(payload, LamasooApiClient.Json));
    }

    [Fact]
    public void DailyPrices_MultipleRooms_KeepSaleCostAndFeeSeparate_WithoutDoubleSummingTotals()
    {
        var offer = Assert.Single(LamasooHotelMapper.Map(Availability(), Inventory, Travel(new HotelParty(2, []), new HotelParty(3, []))));
        Assert.Equal(6200, offer.DisplayPriceMinor);
        Assert.Equal(5616, offer.SupplyPriceMinor);
        Assert.Equal(56, offer.FeeMinor);
        Assert.Equal("agency-native", offer.AgencyProviderId);
        Assert.Equal("BedBreakfast", offer.BoardType);
        Assert.Equal(6200, new HotelSalePriceCalculator().CalculateFinalPrice(offer));
    }

    [Fact]
    public void Children_ArePricedUsingTheirOwnRoomAgesAndBedPolicy()
    {
        var offer = Assert.Single(LamasooHotelMapper.Map(Availability(), Inventory, Travel(new HotelParty(2, [0]), new HotelParty(2, [4]))));
        Assert.Equal(6500, offer.DisplayPriceMinor);
        Assert.Equal(5959, offer.SupplyPriceMinor);
    }

    [Theory]
    [InlineData("night")]
    [InlineData("capacity")]
    [InlineData("child")]
    [InlineData("arrival")]
    [InlineData("stop")]
    [InlineData("minimum")]
    [InlineData("inventory")]
    [InlineData("fee")]
    public void InvalidOrIncompleteOffer_IsNeverOffered(string fault)
    {
        var value = Availability(); var room = value.Hotels[0].RoomTypes[0]; var travel = Travel(new HotelParty(2, []));
        switch (fault)
        {
            case "night": value.Hotels[0].RoomRates[0].Prices.RemoveAt(1); break;
            case "capacity": travel = Travel(new HotelParty(4, [])); break;
            case "child": travel = Travel(new HotelParty(2, [9])); break;
            case "arrival": room.RoomsRemainingPerDay[0].ClosedToArrival = true; break;
            case "stop": room.RoomsRemainingPerDay[1].StopSell = true; break;
            case "minimum": room.RoomsRemainingPerDay[0].MinLos = 4; break;
            case "inventory": room.RoomsRemainingPerDay[1].Count = 0; break;
            case "fee": value.Wage.Type = "unknown"; break;
        }
        Assert.Empty(LamasooHotelMapper.Map(value, Inventory, travel));
    }

    [Fact]
    public void UnknownCurrency_IsRejected()
    {
        var value = Availability(); value.Currency = "USD";
        Assert.Throws<InvalidOperationException>(() => LamasooHotelMapper.Map(value, Inventory, Travel(new HotelParty(2, []))));
    }

    [Theory]
    [InlineData(500)]
    [InlineData(408)]
    public async Task MutationTransientHttpFailure_IsUnknown_AndSentOnlyOnce(int status)
    {
        var handler = new FakeHttpHandler(_ => Task.FromResult(new HttpResponseMessage((HttpStatusCode)status)));
        var client = new LamasooApiClient(new HttpClient(handler) { BaseAddress = new("https://example.org/") }, NullLogger<LamasooApiClient>.Instance);
        await Assert.ThrowsAsync<HotelExternalOutcomeUnknownException>(() => client.SendAsync<LamasooSubmitResponse>(HttpMethod.Post, "booking/async/booking_submit", new { reference_id = "stable" }, true, default));
        Assert.Equal(1, handler.Calls);
    }

    [Fact]
    public async Task MutationTransportFailure_IsUnknown()
    {
        var handler = new FakeHttpHandler(_ => throw new HttpRequestException());
        var client = new LamasooApiClient(new HttpClient(handler) { BaseAddress = new("https://example.org/") }, NullLogger<LamasooApiClient>.Instance);
        await Assert.ThrowsAsync<HotelExternalOutcomeUnknownException>(() => client.SendAsync<LamasooSubmitResponse>(HttpMethod.Post, "booking/async/booking_submit", new { }, true, default));
        Assert.Equal(1, handler.Calls);
    }

    [Fact]
    public async Task MutationMalformedSuccess_IsUnknown()
    {
        var handler = new FakeHttpHandler(_ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("not json") }));
        var client = new LamasooApiClient(new HttpClient(handler) { BaseAddress = new("https://example.org/") }, NullLogger<LamasooApiClient>.Instance);
        await Assert.ThrowsAsync<HotelExternalOutcomeUnknownException>(() => client.SendAsync<LamasooSubmitResponse>(HttpMethod.Post, "booking/async/booking_submit", new { }, true, default));
    }

    [Fact]
    public void UnknownOutcome_CannotBeSubmittedOrConfirmedAgain()
    {
        var row = HotelExternalReservation.Create(Guid.NewGuid(), "Lamasoo", "hash", "{}");
        row.StartSubmit(); row.MarkUnknown();
        Assert.Throws<InvalidOperationException>(row.StartSubmit);
        Assert.Throws<InvalidOperationException>(row.StartConfirm);
        Assert.Equal(HotelExternalState.Unknown, row.State);
    }

    [Fact]
    public void SnappTripRoomAssignmentsAndSeparateNames_ArePreserved()
    {
        var draft = new Refahi.Modules.Hotels.Application.Contracts.Providers.DTOs.BookingDraftDto
        {
            RoomId = 100,
            RoomsCount = 2,
            Guests = [
                new() { FullName = "Multi Part Name Last", FirstName = "Multi Part Name", LastName = "Last", Nationality = "IR", Type = "Adult", Age = 30, RoomIndex = 0 },
                new() { FullName = "Second Last", FirstName = "Second", LastName = "Last", Nationality = "AF", Type = "Adult", Age = 31, RoomIndex = 1 },
                new() { FullName = "Child Last", Type = "Child", Age = 4, RoomIndex = 1 }
            ]
        };
        var rooms = Refahi.Modules.Hotels.Infrastructure.Providers.SnappTrip.SnappTripHotelProvider.MapRooms(draft);
        Assert.Equal(2, rooms.Count); Assert.Single(rooms[0].guests); Assert.Equal(2, rooms[1].guests.Count);
        Assert.Equal(1, rooms[1].children); Assert.True(rooms[1].guests[0].foreigner);
        Assert.Equal("Multi Part Name", rooms[0].guests[0].first_name);
    }

    [Fact]
    public void SnappTripExtraBeds_AreCalculatedForEachRoomFromServerCapacity()
    {
        var draft = new Refahi.Modules.Hotels.Application.Contracts.Providers.DTOs.BookingDraftDto
        {
            RoomId = 100, RoomsCount = 1, RoomAdultCapacity = 2,
            Guests = Enumerable.Range(0, 3).Select(i => new Refahi.Modules.Hotels.Application.Contracts.Providers.DTOs.GuestDto
                { FullName = "Fixture Guest", Type = "Adult", Age = 30, RoomIndex = 0 }).ToArray()
        };
        var room = Assert.Single(Refahi.Modules.Hotels.Infrastructure.Providers.SnappTrip.SnappTripHotelProvider.MapRooms(draft));
        Assert.Equal(1, room.extra_beds); Assert.Equal(3, room.guests.Count);
    }

    [Fact]
    public void PurchaseFlagsAlone_DoNotEnableLamasooWithoutLifecycleAdapters()
    {
        using var cache = new Microsoft.Extensions.Caching.Memory.MemoryCache(new Microsoft.Extensions.Caching.Memory.MemoryCacheOptions());
        var api = new LamasooApiClient(new HttpClient(), NullLogger<LamasooApiClient>.Instance);
        var options = Microsoft.Extensions.Options.Options.Create(new LamasooOptions { SearchEnabled = true, PurchaseEnabled = true, ContractVerified = true });
        var provider = new LamasooHotelProvider(api, options, cache, new Refahi.Modules.Hotels.Infrastructure.Providers.Catalogue.HotelInventoryStore(null!), null!);
        Assert.True(provider.Capabilities.Search); Assert.False(provider.Capabilities.Purchase);
        Assert.False(provider.Capabilities.Inquiry); Assert.False(provider.Capabilities.Cancellation); Assert.False(provider.Capabilities.Voucher);
    }

    [Theory]
    [InlineData(3, "OFFER_UNAVAILABLE")]
    [InlineData(6, "PROVIDER_CONTRACT_UNAVAILABLE")]
    [InlineData(13, "PROVIDER_CREDIT_UNAVAILABLE")]
    public async Task DocumentedRejectionHasInternalCodeAndDurableEvidence(int code, string expected)
    {
        var handler = new FakeHttpHandler(_ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.BadRequest)
            { Content = JsonContent(code) }));
        var client = new LamasooApiClient(new HttpClient(handler) { BaseAddress = new("https://example.org/") }, NullLogger<LamasooApiClient>.Instance);
        var rejection = await Assert.ThrowsAsync<HotelProviderRejectedException>(() => client.SendAsync<LamasooSubmitResponse>(HttpMethod.Post, "booking/async/booking_submit", new { }, true, default));
        Assert.Equal(expected, rejection.Code); Assert.Contains(code.ToString(), rejection.Snapshot); Assert.Equal(1, handler.Calls);
    }

    [Theory]
    [InlineData(10)]
    [InlineData(12)]
    [InlineData(21)]
    public async Task AmbiguousOrDuplicateReferenceCodeNeverAuthorizesRetry(int code)
    {
        var handler = new FakeHttpHandler(_ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.BadRequest) { Content = JsonContent(code) }));
        var client = new LamasooApiClient(new HttpClient(handler) { BaseAddress = new("https://example.org/") }, NullLogger<LamasooApiClient>.Instance);
        await Assert.ThrowsAsync<HotelExternalOutcomeUnknownException>(() => client.SendAsync<LamasooSubmitResponse>(HttpMethod.Post, "booking/async/booking_submit", new { }, true, default));
        Assert.Equal(1, handler.Calls);
    }

    [Theory]
    [InlineData(true, true, true, true)]
    [InlineData(false, true, true, false)]
    [InlineData(true, false, true, false)]
    [InlineData(true, true, false, false)]
    public void PurchaseRequiresAllContractAndLifecycleGates(bool purchase, bool contract, bool ready, bool expected)
    {
        using var cache = new Microsoft.Extensions.Caching.Memory.MemoryCache(new Microsoft.Extensions.Caching.Memory.MemoryCacheOptions());
        var options = Microsoft.Extensions.Options.Options.Create(new LamasooOptions { PurchaseEnabled = purchase, ContractVerified = contract });
        var provider = new LamasooHotelProvider(new(new HttpClient(), NullLogger<LamasooApiClient>.Instance), options, cache,
            new Refahi.Modules.Hotels.Infrastructure.Providers.Catalogue.HotelInventoryStore(null!), null!, [new Lifecycle(ready)]);
        Assert.Equal(expected, provider.Capabilities.Purchase);
        Assert.False(provider.IsOfferEligible(null!, Travel(new HotelParty(2, []))));
    }
    private sealed class Lifecycle(bool ready) : IHotelReservationLifecycleAdapter
    {
        public string Provider => "Lamasoo";
        public bool IsReady => ready;
        public bool IsOfferEligible(ProviderHotelOffer offer, HotelTravel travel, IReadOnlyList<HotelGuest>? guests) => false;
        public Task<HotelInquiryResult> InquireAsync(string referenceId, CancellationToken ct) => Task.FromResult(new HotelInquiryResult(HotelInquiryState.Unsupported));
    }
    private static StringContent JsonContent(int code) => new(System.Text.Json.JsonSerializer.Serialize(new { code }), System.Text.Encoding.UTF8, "application/json");

    private sealed class FakeHttpHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> send) : HttpMessageHandler
    {
        public int Calls { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) { Calls++; return send(request); }
    }
}
