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
        public readonly TpTicketTypeDto TicketType;
        public TpTempShoppingCartDto Cart;
        public TpPurchaseResultDto Purchase;
        public bool TimeoutPatch;
        public int? PatchStatus;
        public int PatchCount;
        public Fixture(int type = 1)
        {
            Settings = new() { Enabled = true, SalesEnabled = true, Tenant = Supplier, SettlementConfirmed = true, BuyPriceConfirmed = true,
                PaymentMethod = 100, BankGateway = 0, AllowedPriceCategoryIds = [PriceCategory], CategoryCodes = new() { [7] = "entertainment" },
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
                else if (r.Path.EndsWith("/events")) payload = new[] { new TpEventSansDto { Events = [new() { EventId = Event, ExcuteTime = DateTimeOffset.UtcNow.AddDays(2).ToString("O"), TicketTypes = null }] } };
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
