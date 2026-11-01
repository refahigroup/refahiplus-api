using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Caching.Memory;
using Refahi.Modules.Entertainments.Application.Contracts;
using Refahi.Modules.Entertainments.Application.Contracts.Providers;
using Refahi.Modules.Entertainments.Application.Contracts.Providers.Exceptions;
using Refahi.Modules.Entertainments.Application.Services;
using Refahi.Modules.Entertainments.Domain;
using Refahi.Modules.Entertainments.Infrastructure.Persistence;
using Refahi.Modules.Entertainments.Infrastructure.Providers.TouristPanel;
using Refahi.Modules.Entertainments.Infrastructure.Providers.TouristPanel.Contracts;
using Xunit;
using static Refahi.Modules.Entertainments.Tests.TouristPanelClientTests;

namespace Refahi.Modules.Entertainments.Tests;

public sealed class TouristPanelPurchaseTests
{
    private sealed class Protector : IEntertainmentSecretProtector
    {
        public string Protect(string value) => Convert.ToBase64String(Encoding.UTF8.GetBytes(value));
        public string Unprotect(string value) => Encoding.UTF8.GetString(Convert.FromBase64String(value));
        public string Hash(string value) => Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    }
    private sealed class Fixture : IDisposable
    {
        public readonly string Program = Guid.NewGuid().ToString(), Supplier = Guid.NewGuid().ToString(), Type = Guid.NewGuid().ToString(),
            PriceCategory = Guid.NewGuid().ToString(), Event = Guid.NewGuid().ToString(), EventType = Guid.NewGuid().ToString(), CartId = Guid.NewGuid().ToString(), Invoice = Guid.NewGuid().ToString();
        public readonly EntertainmentDbContext Db = new(new DbContextOptionsBuilder<EntertainmentDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        public readonly Transport Transport;
        public readonly TouristPanelProvider Provider;
        public readonly TouristPanelOptions Settings;
        public TpTicketTypeDto TicketType;
        public TpTempShoppingCartDto Cart;
        public TpPurchaseResultDto Purchase;
        public List<TpTicketTypeDto>? EmbeddedTicketTypes;
        public bool TimeoutPatch;
        public int? PatchStatus;
        public int PatchCount;
        public Fixture(int type = 1, TouristPanelOptions? settings = null)
        {
            Settings = settings ?? new() { Enabled = true, SalesEnabled = true, Tenant = Supplier, SettlementConfirmed = true, BuyPriceConfirmed = true,
                PaymentMethod = 100, BankGateway = 0, CategoryCodes = new() { [7] = "entertainment" },
                MarkupPercent = 10, MarkupFixedMinor = 5, PricingVersion = "v1", DeliveryHosts = ["tickets.example.test"],
                ManifestConfirmed = true, DateRangeConfirmed = true, TimeZoneId = "Asia/Tehran" };
            TicketType = new() { ProgramTicketTypeId = Type, EventTicketTypeId = type == 2 ? EventType : null,
                Title = "VIP", RemainingCapacity = 5, TicketPrices = [new() { BuyPrice = 105, TicketPrice = 150,
                    CommissionPriceCategory = new() { CommissionPriceCategoryId = PriceCategory, Currency = TpCurrencyType.IRR } }] };
            var program = new TpProgramDto { Id = Program, SupplyChainHojreId = Supplier, Type = (TpProgramType)type,
                Title = "خدمت", IsActive = true, IsManifestNeeded = true, IsManifestUniqueNumberNeeded = true, ProgramCategory = new() { Code = 7 } };
            Cart = new() { Id = CartId, ExpireDateTime = DateTimeOffset.UtcNow.AddMinutes(15).ToString("O"), TotalAmount = 300,
                Groups = [new() { GroupId = Guid.NewGuid().ToString(), ProgramId = Program, SupplyChainHojreId = Supplier,
                    ProgramTicketTypeId = Type, EventId = type == 2 ? Event : null, EventTicketTypeId = type == 2 ? EventType : null,
                    CommissionPriceCategoryId = PriceCategory, TicketsCount = 2, TotalTicketPrice = 300, TotalBuyPrice = 210,
                    ExpireTime = DateTimeOffset.UtcNow.AddMinutes(10).ToString("O"), Tickets = [Temp("one"), Temp("two")] }] };
            Purchase = new() { Status = true, PurchaseStatus = TpPurchaseStatus.Payed, TicketsUrl = "https://tickets.example.test/secret",
                TicketsInvoice = new() { Id = Invoice, PurchaseStatus = TpPurchaseStatus.Payed, TotalSupplierBuyAmount = 210,
                    TicketGroups = [new() { GroupId = Cart.Groups[0].GroupId, ProgramId = Program, SupplyChainHojreId = Supplier, ProgramTicketTypeId = Type,
                        EventId = type == 2 ? Event : null, EventTicketTypeId = type == 2 ? EventType : null, ProgramTicketTypeTitle = "VIP",
                        Tickets = [Ticket("one"), Ticket("two")] }] } };
            Transport = new(r =>
            {
                object payload;
                if (r.Path.EndsWith("connect/token")) return Json("{\"access_token\":\"token\",\"token_type\":\"Bearer\",\"expires_in\":3600}");
                if (r.Method == "PATCH")
                {
                    PatchCount++;
                    if (TimeoutPatch) throw new HttpRequestException("connection dropped after send");
                    if (PatchStatus.HasValue) return Json("{}", (HttpStatusCode)PatchStatus.Value);
                    payload = Purchase;
                }
                else if (r.Path.EndsWith("purchase-result")) payload = Purchase;
                else if (r.Method == "DELETE") payload = false;
                else if (r.Path.EndsWith("program/detail")) payload = program;
                else if (r.Path.EndsWith("ticket-types")) payload = new[] { TicketType };
                else if (r.Path.EndsWith("/events")) payload = new[] { new TpEventSansDto { Events = [new() { EventId = Event, ExcuteTime = DateTimeOffset.UtcNow.AddDays(2).ToString("O"), TicketTypes = EmbeddedTicketTypes }] } };
                else payload = Cart;
                return Json(JsonSerializer.Serialize(payload));
            });
            var client = Client(Transport, Settings);
            Provider = new(client, new(Db, client, Options.Create(Settings), Options.Create(new Refahi.Modules.Entertainments.Application.Contracts.Discovery.EntertainmentsOptions()), new FakeMedia(), Microsoft.Extensions.Logging.Abstractions.NullLogger<TouristPanelCatalog>.Instance), new(new MemoryCache(new MemoryCacheOptions()), client, Options.Create(Settings)),
                Options.Create(Settings), new EntertainmentPricingService(), Db, new Protector(), new FakeDiscovery(Program));
        }
        private TpTempShoppingCartTicketDto Temp(string name) => new() { TicketId = Guid.NewGuid().ToString(), BuyPrice = 105, TicketPrice = 150, ManifestUniqueName = name, ManifestUniqueNumber = name + "-id" };
        private TpTicketDto Ticket(string name) => new() { Id = Guid.NewGuid().ToString(), TicketNo = name + "-ticket", CommissionPriceCategoryId = PriceCategory, SupplierBuyAmount = 105, ManifestUniqueName = name, ManifestUniqueNumber = name + "-id" };
        public EntertainmentReservationRequest Request(int type = 1) => new(Guid.NewGuid().ToString("N"), "خریدار", "09120000000",
            [new(Guid.NewGuid(), Program, type == 2 ? Event : "service:" + Program, TouristPanelProvider.OptionKey(TicketType, TicketType.TicketPrices![0]), 2, 100,
                [new("one", "one-id"), new("two", "two-id")])]);
        public async Task<EntertainmentFulfillmentResult> Issue(string operation = "stable")
        {
            var reservation = await Provider.ReserveAsync(Request(), default);
            return await Provider.FulfillAsync(new(operation, "خریدار", "09120000000", []) { ReservationContext = reservation.ProtectedContext, ReservationReference = reservation.Reference }, default);
        }
        public void Dispose() => Db.Dispose();
    }

    private static TouristPanelOptions RetailSettings() => new()
    {
        Enabled = true, SalesEnabled = true, UseProviderSalePrice = true, MarkupPercent = 0, MarkupFixedMinor = 0,
        DiscountPercent = 0, DiscountFixedMinor = 0, PricingVersion = "local-touristpanel-retail-v1",
        SettlementConfirmed = true, BuyPriceConfirmed = true, PaymentMethod = 100, BankGateway = 0,
        ManifestConfirmed = true, ReferenceTicketsConfirmed = true, DateRangeConfirmed = true,
        TimeZoneId = "Asia/Tehran", DeliveryHosts = ["tickets.example.test"]
    };

    [Theory][InlineData(1)][InlineData(2)]
    public async Task Provider_retail_mode_charges_ticket_price_without_refahi_adjustments_and_preserves_native_cost(int type)
    {
        using var f = new Fixture(type, RetailSettings());
        var day = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(2));
        var option = Assert.Single(Assert.Single(await f.Provider.GetOffersAsync(f.Program, day, day, default)).PurchaseOptions);
        Assert.True(option.IsAvailable); Assert.Equal(150, option.PriceMinor);
        var result = await f.Provider.ReserveAsync(f.Request(type), default);
        var quote = Assert.Single(result.Lines).Quote;
        Assert.Equal(150, quote.UnitPriceMinor); Assert.Equal(105, quote.ProviderCostMinor);
        Assert.Equal(0, quote.MarkupMinor); Assert.Equal(0, quote.DiscountMinor); Assert.Equal(45, quote.ProviderRetailAdjustmentMinor);
        Assert.Contains("ProviderRetail", quote.ProviderPayloadJson);
        var issued = await f.Provider.FulfillAsync(new("retail-issue", "خریدار", "09120000000", [])
            { ReservationContext = result.ProtectedContext, ReservationReference = result.Reference }, default);
        Assert.Equal("Completed", issued.Status); Assert.Equal(1, f.PatchCount);
    }

    [Fact]
    public async Task Provider_retail_mode_rejects_a_reservation_whose_ticket_price_changed()
    {
        using var f = new Fixture(settings: RetailSettings());
        f.Cart = f.Cart with { Groups = [f.Cart.Groups![0] with { TotalTicketPrice = 302,
            Tickets = f.Cart.Groups[0].Tickets!.Select(t => t with { TicketPrice = 151 }).ToList() }], TotalAmount = 302 };
        await Assert.ThrowsAsync<EntertainmentProviderAmbiguousException>(() => f.Provider.ReserveAsync(f.Request(), default));
        Assert.Equal(0, f.PatchCount);
    }

    [Theory][InlineData(0)][InlineData(-1)][InlineData(1.5)]
    public async Task Provider_retail_mode_rejects_invalid_ticket_price(decimal retail)
    {
        using var f = new Fixture(settings: RetailSettings());
        f.TicketType = f.TicketType with { TicketPrices = [f.TicketType.TicketPrices![0] with { TicketPrice = retail }] };
        var option = Assert.Single(Assert.Single(await f.Provider.GetOffersAsync(f.Program, null, null, default)).PurchaseOptions);
        Assert.False(option.IsAvailable); Assert.Equal("PriceUnavailable", option.UnavailableReasonCode);
    }

    [Fact]
    public async Task Selected_date_uses_dedicated_ticket_prices_instead_of_different_embedded_event_prices()
    {
        using var f = new Fixture(2, RetailSettings());
        f.EmbeddedTicketTypes = [f.TicketType with { TicketPrices = [f.TicketType.TicketPrices![0] with { BuyPrice = 999, TicketPrice = 1200 }] }];
        var day = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(2));
        var option = Assert.Single(Assert.Single(await f.Provider.GetOffersAsync(f.Program, day, day, default)).PurchaseOptions);
        var quote = await f.Provider.QuoteAsync(new(f.Program, f.Event, option.Key, 1), default);
        Assert.Equal(150, option.PriceMinor); Assert.Equal(option.PriceMinor, quote.UnitPriceMinor);
        Assert.Equal(105, quote.ProviderCostMinor);
        Assert.Contains(f.Transport.Requests, r => r.Path.EndsWith("event/ticket-types"));
    }

    [Fact]
    public async Task Recorded_live_prices_are_exposed_without_registering_provider_categories()
    {
        using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "touristpanel-live-prices.json")));
        foreach (var sample in document.RootElement.GetProperty("samples").EnumerateArray())
        {
            using var f = new Fixture();
            var price = sample.GetProperty("price").Deserialize<TpTicketPriceDto>()!;
            f.TicketType = f.TicketType with { Title = sample.GetProperty("ticket").GetString(), ReferenceTicketTypeId = Guid.Empty.ToString(), TicketPrices = [price] };
            var option = Assert.Single(Assert.Single(await f.Provider.GetOffersAsync(f.Program, null, null, default)).PurchaseOptions);
            Assert.True(option.IsAvailable); Assert.Equal(f.TicketType.Title, option.Title);
            var quote = await f.Provider.QuoteAsync(new(f.Program, "service:" + f.Program, option.Key, 1), default);
            Assert.Equal(checked((long)price.BuyPrice), quote.ProviderCostMinor);
            Assert.Contains("providerTicketPrice", quote.ProviderPayloadJson); Assert.Contains("providerCommissionPrice", quote.ProviderPayloadJson);
            Assert.Contains("discountMinor", quote.ProviderPayloadJson); Assert.Contains("pricingVersion", quote.ProviderPayloadJson);
        }
    }

    [Fact]
    public async Task Empty_reference_sentinel_is_optional_only_when_no_reference_dependency_is_required()
    {
        using var f = new Fixture();
        f.TicketType = f.TicketType with { ReferenceTicketTypeId = Guid.Empty.ToString() };
        var option = Assert.Single(Assert.Single(await f.Provider.GetOffersAsync(f.Program, null, null, default)).PurchaseOptions);
        Assert.True(option.IsAvailable);
        var reservation = await f.Provider.ReserveAsync(f.Request(), default);
        Assert.Single(reservation.Lines);
        var add = Assert.Single(f.Transport.Requests, r => r.Method == "POST" && r.Path.EndsWith("/shopping-cart"));
        Assert.DoesNotContain("referenceTicketTypeId", add.Body!);
        f.TicketType = f.TicketType with { IsNumberEqualsReferenceTicketType = true };
        await Assert.ThrowsAsync<EntertainmentDomainException>(() => f.Provider.QuoteAsync(new(f.Program, "service:" + f.Program, option.Key, 1), default));
    }

    [Fact]
    public async Task Cheapest_price_is_shared_by_offers_quote_and_reservation_without_allowlist()
    {
        using var f = new Fixture();
        var original = f.TicketType.TicketPrices![0];
        var other = original with { BuyPrice = 200, CommissionPriceCategory = original.CommissionPriceCategory! with
            { CommissionPriceCategoryId = Guid.NewGuid().ToString(), Title = "internal category" } };
        f.TicketType = f.TicketType with { TicketPrices = [other, original, original with { BuyPrice = 105.0m, CommissionPriceCategory = original.CommissionPriceCategory! with { CommissionPriceCategoryId = f.PriceCategory.ToUpperInvariant() } }] };
        f.Settings.CategoryCodes.Clear();
        var option = Assert.Single(Assert.Single(await f.Provider.GetOffersAsync(f.Program, null, null, default)).PurchaseOptions);
        Assert.True(option.IsAvailable); Assert.Equal("VIP", option.Title);
        var quote = await f.Provider.QuoteAsync(new(f.Program, "service:" + f.Program, option.Key, 2), default);
        Assert.Equal("entertainment", quote.CategoryCode); Assert.Equal(105, quote.ProviderCostMinor);
        Assert.Equal(option.PriceMinor, quote.UnitPriceMinor);
        // A legacy key for a more expensive price still resolves the type and selects today's cheapest price.
        var request = f.Request();
        request = request with { Lines = [request.Lines[0] with { PurchaseOptionKey = TouristPanelProvider.OptionKey(f.TicketType, other) }] };
        var reservation = await f.Provider.ReserveAsync(request, default);
        Assert.Equal(option.Key, Assert.Single(reservation.Lines).Quote.PurchaseOptionKey);
        var add = Assert.Single(f.Transport.Requests, r => r.Method == "POST" && r.Path.EndsWith("/shopping-cart"));
        Assert.Equal(f.PriceCategory, JsonDocument.Parse(add.Body!).RootElement.GetProperty("tickets")[0].GetProperty("commissionPriceCategoryId").GetString());
    }

    [Fact]
    public async Task Reservation_merges_legacy_category_rows_without_losing_quantities_or_passengers()
    {
        using var f = new Fixture();
        var original = f.TicketType.TicketPrices![0];
        var other = original with { BuyPrice = 200, CommissionPriceCategory = original.CommissionPriceCategory! with { CommissionPriceCategoryId = Guid.NewGuid().ToString() } };
        f.TicketType = f.TicketType with { TicketPrices = [original, other] };
        var request = f.Request(); var line = request.Lines[0];
        request = request with { Lines = [line with { Quantity = 1, Passengers = [line.Passengers[0]] },
            line with { CartItemId = Guid.NewGuid(), PurchaseOptionKey = TouristPanelProvider.OptionKey(f.TicketType, other), Quantity = 1, Passengers = [line.Passengers[1]] }] };
        var result = await f.Provider.ReserveAsync(request, default);
        var merged = Assert.Single(result.Lines); Assert.Equal(2, merged.Quote.Quantity); Assert.Equal(2, merged.Passengers.Count);
        Assert.Contains(merged.Passengers, x => x.Name == "one"); Assert.Contains(merged.Passengers, x => x.Name == "two");
        Assert.Single(f.Transport.Requests, r => r.Method == "POST" && r.Path.EndsWith("/shopping-cart"));
    }

    [Fact]
    public async Task Ranking_ties_are_deterministic_and_identity_survives_category_and_cost_changes()
    {
        using var f = new Fixture();
        var original = f.TicketType.TicketPrices![0];
        var lowerId = original with { CommissionPriceCategory = original.CommissionPriceCategory! with
            { CommissionPriceCategoryId = "00000000-0000-0000-0000-000000000001" } };
        f.TicketType = f.TicketType with { TicketPrices = [original, lowerId] };
        var key = TouristPanelProvider.StableOptionKey(f.Program, "service:" + f.Program, f.TicketType);
        var first = await f.Provider.QuoteAsync(new(f.Program, "service:" + f.Program, key, 1), default);
        Assert.Contains("00000000-0000-0000-0000-000000000001", first.ProviderPayloadJson);
        f.TicketType = f.TicketType with { TicketPrices = [lowerId, original with { BuyPrice = 99 }] };
        var second = await f.Provider.QuoteAsync(new(f.Program, "service:" + f.Program, key, 1), default);
        Assert.Equal(first.PurchaseOptionKey, second.PurchaseOptionKey); Assert.Equal(99, second.ProviderCostMinor);
        Assert.NotEqual(first.UnitPriceMinor, second.UnitPriceMinor);
    }

    [Theory]
    [InlineData("empty")][InlineData("zero")][InlineData("negative")][InlineData("fraction")]
    [InlineData("overflow")][InlineData("currency")][InlineData("identity")][InlineData("conflict")]
    public async Task Invalid_prices_produce_an_unavailable_option_and_never_reserve(string kind)
    {
        using var f = new Fixture();
        var original = f.TicketType.TicketPrices![0];
        var invalid = kind switch
        {
            "zero" => original with { BuyPrice = 0 },
            "negative" => original with { BuyPrice = -1 },
            "fraction" => original with { BuyPrice = 1.5m },
            "overflow" => original with { BuyPrice = decimal.MaxValue },
            "currency" => original with { CommissionPriceCategory = original.CommissionPriceCategory! with { Currency = TpCurrencyType.USD } },
            "identity" => original with { CommissionPriceCategory = original.CommissionPriceCategory! with { CommissionPriceCategoryId = "invalid" } },
            _ => original
        };
        f.TicketType = f.TicketType with { TicketPrices = kind == "empty" ? [] : kind == "conflict" ? [original, original with { BuyPrice = 104 }] : [invalid] };
        var option = Assert.Single(Assert.Single(await f.Provider.GetOffersAsync(f.Program, null, null, default)).PurchaseOptions);
        Assert.False(option.IsAvailable); Assert.Equal("PriceUnavailable", option.UnavailableReasonCode);
        await Assert.ThrowsAsync<EntertainmentDomainException>(() => f.Provider.QuoteAsync(new(f.Program, "service:" + f.Program, option.Key, 1), default));
        Assert.DoesNotContain(f.Transport.Requests, r => r.Method == "POST" && r.Path.EndsWith("shopping-cart"));
    }

    [Theory][InlineData(1, "disabled", "TicketDisabled")][InlineData(2, "capacity", "SoldOut")]
    public async Task Availability_has_a_public_reason(int type, string condition, string reason)
    {
        using var f = new Fixture(type);
        f.TicketType = f.TicketType with { IsDisabled = condition == "disabled", RemainingCapacity = condition == "capacity" ? 0 : 5 };
        var day = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(2));
        var option = Assert.Single(Assert.Single(await f.Provider.GetOffersAsync(f.Program, day, day, default)).PurchaseOptions);
        Assert.False(option.IsAvailable); Assert.Equal(reason, option.UnavailableReasonCode); Assert.True(option.PriceMinor > 0);
    }

    [Fact]
    public async Task Connection_only_mode_can_display_prices_without_enabling_sales()
    {
        using var f = new Fixture(settings: new TouristPanelOptions { MarkupPercent = 0, MarkupFixedMinor = 0, PricingVersion = "display-v1" });
        var option = Assert.Single(Assert.Single(await f.Provider.GetOffersAsync(f.Program, null, null, default)).PurchaseOptions);
        Assert.Equal(105, option.PriceMinor); Assert.False(option.IsAvailable); Assert.Equal("SalesUnavailable", option.UnavailableReasonCode);
    }

    [Fact]
    public async Task Reserved_purchase_keeps_original_context_after_prices_change()
    {
        using var f = new Fixture();
        var reservation = await f.Provider.ReserveAsync(f.Request(), default);
        f.TicketType = f.TicketType with { TicketPrices = [f.TicketType.TicketPrices![0] with { BuyPrice = 999 }] };
        var changedSettings = JsonSerializer.Deserialize<TouristPanelOptions>(JsonSerializer.Serialize(f.Settings)
            .Replace("\"MarkupPercent\":10", "\"MarkupPercent\":99")
            .Replace("\"PricingVersion\":\"v1\"", "\"PricingVersion\":\"v2\""))!;
        Assert.Equal("v2", changedSettings.PricingVersion);
        var client = Client(f.Transport, changedSettings);
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var changedProvider = new TouristPanelProvider(client, null!, new(cache, client, Options.Create(changedSettings)),
            Options.Create(changedSettings), new EntertainmentPricingService(), f.Db, new Protector(), new FakeDiscovery(f.Program));
        var result = await changedProvider.FulfillAsync(new("pinned-price", "خریدار", "09120000000", [])
            { ReservationContext = reservation.ProtectedContext, ReservationReference = reservation.Reference }, default);
        Assert.Equal("Completed", result.Status); Assert.Equal(1, f.PatchCount);
    }

    [Fact]
    public void Cart_total_overflow_is_rejected_before_mutating_existing_rows()
    {
        var cart = EntertainmentCart.Create(Guid.NewGuid());
        var item = new EntertainmentCartItemSnapshot("touristpanel", "supplier", "program", "offer", "one", "title", "supplier", "program", null, "ticket", "offer", 1, long.MaxValue, long.MaxValue);
        cart.AddOrReplace(item);
        Assert.Throws<EntertainmentDomainException>(() => cart.AddOrReplace(item with { PurchaseOptionKey = "two", ExpectedUnitPriceMinor = 1 }));
        Assert.Throws<EntertainmentDomainException>(() => cart.UpdateQuantity(cart.Items[0].Id, 2));
        Assert.Single(cart.Items); Assert.Equal(1, cart.Items[0].Quantity);
    }

    [Fact]
    public void Legacy_cart_rows_merge_into_one_stable_selection_without_losing_the_row_id()
    {
        var cart = EntertainmentCart.Create(Guid.NewGuid());
        var old = new EntertainmentCartItemSnapshot("touristpanel", "supplier", "program", "offer", "old1", "title", "supplier", "program", null, "ticket", "offer", 1, 100, 100);
        cart.AddOrReplace(old); var rowId = cart.Items[0].Id;
        cart.AddOrReplace(old with { PurchaseOptionKey = "old2" });
        cart.AddOrReplace(old with { PurchaseOptionKey = "stable", Quantity = 2 }, ["old1", "old2"]);
        var row = Assert.Single(cart.Items); Assert.Equal(rowId, row.Id); Assert.Equal("stable", row.PurchaseOptionKey); Assert.Equal(2, row.Quantity);
    }

    [Fact]
    public async Task Public_guid_formats_share_one_canonical_cart_identity()
    {
        using var f = new Fixture();
        var option = TouristPanelProvider.OptionKey(f.TicketType, f.TicketType.TicketPrices![0]);
        var first = await f.Provider.QuoteAsync(new(f.Program, "service:" + f.Program, option, 1), default);
        var second = await f.Provider.QuoteAsync(new(Guid.Parse(f.Program).ToString("N"), "service:" + f.Program, option, 1), default);
        Assert.Equal(first.ProgramKey, second.ProgramKey);
        Assert.Equal(Guid.Parse(f.Program).ToString("N"), first.ProgramKey);
    }
    [Fact]
    public async Task Checkout_requirements_are_read_from_current_provider_data()
    {
        using var fixture = new Fixture();
        var requirements = await fixture.Provider.GetRequirementsAsync(fixture.Program, default);
        Assert.True(requirements.RequiresManifest); Assert.True(requirements.RequiresIdentityNumber);
    }
    [Theory] [InlineData(1)] [InlineData(2)]
    public async Task Reservation_uses_exact_passenger_count_fresh_cost_and_earliest_expiry(int type)
    {
        using var f = new Fixture(type);
        var reserved = await f.Provider.ReserveAsync(f.Request(type), default);
        var line = Assert.Single(reserved.Lines);
        Assert.Equal(121, line.Quote.UnitPriceMinor); Assert.Equal(105, line.Quote.ProviderCostMinor); Assert.Equal(16, line.Quote.MarkupMinor);
        Assert.Equal(DateTimeOffset.Parse(f.Cart.Groups![0].ExpireTime!).AddSeconds(-60), reserved.PayableUntil);
        Assert.Equal(64, line.Quote.PurchaseOptionKey.Length);
        var add = Assert.Single(f.Transport.Requests, r => r.Method == "POST" && r.Path.EndsWith("/shopping-cart"));
        var tickets = JsonDocument.Parse(add.Body!).RootElement.GetProperty("tickets"); Assert.Equal(2, tickets.GetArrayLength());
        Assert.Equal("one", tickets[0].GetProperty("manifestUniqueName").GetString()); Assert.Equal("two-id", tickets[1].GetProperty("manifestUniqueNumber").GetString());
        Assert.DoesNotContain("one-id", reserved.ProtectedContext);
    }
    [Fact]
    public async Task Successful_purchase_replays_by_invoice_without_second_patch()
    {
        using var f = new Fixture(); var result = await f.Issue();
        Assert.Equal("Completed", result.Status); Assert.Equal(2, result.Tickets.Count); Assert.All(result.Tickets, x => Assert.Equal("VIP", x.Title));
        Assert.Equal(f.Invoice, result.ProviderInvoiceId); Assert.Single(result.Documents);
        var replay = await f.Provider.FulfillAsync(new("stable", "changed", "changed", []), default);
        Assert.Equal("Completed", replay.Status); Assert.Equal(1, f.PatchCount);
        Assert.Contains(f.Transport.Requests, r => r.Path.EndsWith("purchase-result"));
    }
    [Fact]
    public async Task Timeout_without_invoice_requires_review_and_never_repeats_purchase()
    {
        using var f = new Fixture { TimeoutPatch = true };
        await Assert.ThrowsAsync<EntertainmentProviderAmbiguousException>(() => f.Issue());
        var status = await f.Provider.GetStatusAsync("stable", null, default);
        Assert.Equal("ManualReview", status.Status);
        await f.Provider.FulfillAsync(new("stable", "x", "x", []), default);
        Assert.Equal(1, f.PatchCount);
    }
    [Theory] [InlineData(408)] [InlineData(409)] [InlineData(422)]
    public async Task Business_http_errors_without_definite_nonissuance_are_not_refundable(int code)
    {
        using var f = new Fixture { PatchStatus = code };
        await Assert.ThrowsAsync<TouristPanelHttpException>(() => f.Issue());
        Assert.Equal("ManualReview", (await f.Provider.GetStatusAsync("stable", null, default)).Status);
        Assert.Equal(1, f.PatchCount);
    }
    [Fact]
    public async Task Pending_invoice_can_complete_in_background()
    {
        using var f = new Fixture(); var paid = f.Purchase;
        f.Purchase = paid with { PurchaseStatus = TpPurchaseStatus.PaymentWaiting, TicketsInvoice = paid.TicketsInvoice! with { PurchaseStatus = TpPurchaseStatus.PaymentWaiting } };
        Assert.Equal("Pending", (await f.Issue()).Status);
        f.Purchase = paid;
        Assert.Equal("Completed", (await f.Provider.GetStatusAsync("stable", f.Invoice, default)).Status);
        Assert.Equal(1, f.PatchCount);
    }
    [Fact]
    public async Task Wrong_passenger_or_cost_blocks_delivery_and_refund()
    {
        using var f = new Fixture();
        f.Purchase = f.Purchase with { TicketsInvoice = f.Purchase.TicketsInvoice! with { TotalSupplierBuyAmount = 211 } };
        Assert.Equal("ManualReview", (await f.Issue()).Status);
    }
    [Fact]
    public async Task Changed_cart_cost_is_rejected_before_patch()
    {
        using var f = new Fixture();
        f.Cart = f.Cart with { Groups = [f.Cart.Groups![0] with { TotalBuyPrice = 211 }] };
        await Assert.ThrowsAsync<EntertainmentProviderAmbiguousException>(() => f.Issue()); Assert.Equal(0, f.PatchCount);
    }
    [Fact]
    public async Task Delete_false_is_business_failure()
    {
        using var f = new Fixture(); await Assert.ThrowsAsync<EntertainmentProviderAmbiguousException>(() => f.Provider.ReleaseAsync(f.CartId, default));
    }
    [Theory] [InlineData("http://tickets.example.test/x")] [InlineData("https://evil.example.test/x")] [InlineData("javascript:alert(1)")]
    public async Task Untrusted_delivery_links_are_not_exposed(string url)
    {
        using var f = new Fixture(); f.Purchase = f.Purchase with { TicketsUrl = url };
        var result = await f.Issue(); Assert.Equal("Completed", result.Status); Assert.Empty(result.Documents);
    }
    [Fact]
    public async Task Null_event_ticket_types_are_loaded_only_for_selected_range()
    {
        using var f = new Fixture(2); var day = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(2));
        var offers = await f.Provider.GetOffersAsync(f.Program, day, day, default);
        Assert.Single(Assert.Single(offers).PurchaseOptions);
        Assert.Contains(f.Transport.Requests, r => r.Path.EndsWith("event/ticket-types"));
    }
}
