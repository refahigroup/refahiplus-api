using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Refahi.Modules.Commerce.Infrastructure.Providers.TouristPanel;
using Refahi.Modules.Commerce.Infrastructure.Providers.TouristPanel.Contracts;
using Refahi.Modules.Commerce.Application.Contracts.Providers.Exceptions;
using Xunit;

namespace Refahi.Modules.Commerce.Tests;

public sealed class TouristPanelClientTests
{
    private const string Id = "12345678-1234-1234-1234-123456789abc";
    private const string Other = "12345678-1234-1234-1234-123456789def";
    internal static TouristPanelOptions Connection() => new() { Tenant = Id, ClientId = "client", ClientSecret = "a&b=c +", Username = "user", Password = "p&=+" };
    internal sealed record Request(string Method, string Path, string Query, string? Body, string? Token, string Tenant);
    internal sealed class Transport(Func<Request, HttpResponseMessage> respond) : HttpMessageHandler
    {
        public List<Request> Requests { get; } = [];
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage message, CancellationToken ct)
        {
            var r = new Request(message.Method.Method, message.RequestUri!.AbsolutePath, message.RequestUri.Query,
                message.Content == null ? null : await message.Content.ReadAsStringAsync(ct), message.Headers.Authorization?.Parameter,
                message.Headers.GetValues("__tenant").Single());
            lock (Requests) Requests.Add(r);
            return respond(r);
        }
    }
    internal static HttpResponseMessage Json(string body, HttpStatusCode status = HttpStatusCode.OK) => new(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
    internal static TouristPanelClient Client(Transport transport, TouristPanelOptions? settings = null)
    {
        var options = Options.Create(settings ?? Connection());
        var http = new HttpClient(transport);
        return new(http, new TouristPanelTokenProvider(http, options), options, NullLogger<TouristPanelClient>.Instance);
    }
    private static string Response(Request r) => r.Path.EndsWith("connect/token") ? "{\"access_token\":\"token\",\"token_type\":\"Bearer\",\"expires_in\":3600}"
        : r.Method == "DELETE" ? "true" : r.Path.EndsWith("native-app-version") ? "\"3.7.35\""
        : new[] { "/locations", "/categories", "/programs", "/ticket-types", "/events" }.Any(r.Path.EndsWith) ? "[]" : "{}";

    [Fact]
    public async Task All_hub_operations_preserve_wire_names_repeated_queries_and_get_body()
    {
        var transport = new Transport(r => Json(Response(r))); var c = Client(transport); var ct = CancellationToken.None;
        await c.GetSettingsAsync(ct); await c.GetNativeVersionAsync(ct); await c.GetLocationsAsync(ct);
        await c.GetCategoriesAsync("IR +&", ct); await c.GetProgramsAsync(2, 20, "IR", [1, 2], ct);
        await c.GetProgramAsync(Id, ct); await c.GetDetailAsync(Id, [Id, Other], ct);
        await c.GetProgramTicketsAsync(Id, Other, ct);
        await c.GetEventsAsync(Id, Other, new(2026, 9, 6), new(2026, 9, 7), [Id, Other], true, ct);
        await c.GetEventTicketsAsync(Id, Other, [Id, Other], ct);
        var group = new TpAddTempGroupDto { ProgramId = Id, SupplyChainHojreId = Other, Tickets = [new() { ProgramTicketTypeId = Id, ManifestUniqueName = "مسافر" }] };
        await c.AddGroupAsync(group, null, null, ct); await c.AddGroupsAsync([group], Id, ct); await c.GetCartAsync(Id, ct);
        Assert.True(await c.DeleteCartAsync(Id, ct)); Assert.True(await c.DeleteGroupAsync(Id, Other, ct)); Assert.True(await c.DeleteTicketAsync(Id, Other, ct));
        var final = new TpFinalizingTempShoppingCartDto { PaymentMethod = TpPaymentMethod.Marketplace_Wallets, BankGateway = TpPaymentGatewayType.None, TotalDiscount = 0, SendTicketSms = false };
        await c.FinalizeAsync(Id, final, Other, null, ct); await c.CheckDiscountAsync(Id, "x+y &", final, ct); await c.GetPurchaseAsync(Id, ct);
        Assert.Equal(20, transport.Requests.Count); // token + 17 operations + two narrower DELETE variants
        Assert.All(transport.Requests, r => Assert.Equal(Id, r.Tenant));
        Assert.All(transport.Requests.Skip(1), r => Assert.Equal("token", r.Token));
        var token = System.Web.HttpUtility.ParseQueryString(transport.Requests[0].Body!);
        Assert.Equal("a&b=c +", token["client_secret"]); Assert.Equal("p&=+", token["password"]); Assert.Equal("password", token["grant_type"]);
        var programs = transport.Requests.Single(r => r.Path.EndsWith("/programs"));
        Assert.Contains("categoryCodes=1&categoryCodes=2", programs.Query);
        var events = transport.Requests.Single(r => r.Path.EndsWith("/events"));
        Assert.Contains("icloudTicketTypes=true", events.Query); Assert.Contains("start=2026-09-06&end=2026-09-07", events.Query);
        Assert.Equal(2, System.Web.HttpUtility.ParseQueryString(events.Query).GetValues("filteredProgramTicketTypes")!.Length);
        var add = transport.Requests.Single(r => r.Method == "POST" && r.Path.EndsWith("/shopping-cart"));
        Assert.Equal("", add.Query); Assert.False(JsonDocument.Parse(add.Body!).RootElement.TryGetProperty("quantity", out _));
        var deletes = transport.Requests.Where(r => r.Method == "DELETE").ToArray();
        Assert.DoesNotContain("groupId", deletes[0].Query); Assert.DoesNotContain("ticketId", deletes[0].Query);
        Assert.Contains("groupId=", deletes[1].Query); Assert.DoesNotContain("ticketId=", deletes[1].Query);
        Assert.Contains("ticketId=", deletes[2].Query); Assert.DoesNotContain("groupId=", deletes[2].Query);
        var discount = transport.Requests.Single(r => r.Path.EndsWith("check-discount-code"));
        Assert.Equal("GET", discount.Method); Assert.NotNull(discount.Body);
        Assert.Equal("x+y &", System.Web.HttpUtility.ParseQueryString(discount.Query)["discountCode"]);
        var patch = transport.Requests.Single(r => r.Method == "PATCH");
        Assert.Contains("cashDeskId=", patch.Query); Assert.DoesNotContain("discountCode", patch.Query);
        var root = JsonDocument.Parse(patch.Body!).RootElement;
        Assert.Equal(100, root.GetProperty("paymentMethod").GetInt32()); Assert.False(root.GetProperty("sendTicketSms").GetBoolean());
        Assert.Equal(0, root.GetProperty("totalDiscount").GetDecimal());
    }

    [Fact]
    public async Task Token_refresh_is_single_flight_and_get_retries_only_once_after_401()
    {
        var n = 0;
        var transport = new Transport(r => r.Path.EndsWith("connect/token") ? Json($"{{\"access_token\":\"t{Interlocked.Increment(ref n)}\",\"token_type\":\"Bearer\",\"expires_in\":3600}}")
            : r.Token == "t1" ? Json("{}", HttpStatusCode.Unauthorized) : Json("{}"));
        var c = Client(transport);
        await c.GetSettingsAsync(default);
        await Task.WhenAll(Enumerable.Range(0, 20).Select(_ => c.GetSettingsAsync(default)));
        Assert.Equal(2, n); Assert.Equal(22, transport.Requests.Count(r => r.Method == "GET"));
    }

    [Theory]
    [InlineData(401)] [InlineData(408)] [InlineData(409)] [InlineData(500)]
    public async Task Finalization_is_never_retried(int status)
    {
        var transport = new Transport(r => Json(r.Path.EndsWith("connect/token") ? Response(r) : "{}", r.Path.EndsWith("connect/token") ? HttpStatusCode.OK : (HttpStatusCode)status));
        await Assert.ThrowsAnyAsync<Exception>(() => Client(transport).FinalizeAsync(Id, new(), null, null, default));
        Assert.Single(transport.Requests, r => r.Method == "PATCH");
    }

    [Theory]
    [InlineData("")] [InlineData("<html>error</html>")] [InlineData("{\"id\":")]
    public async Task Malformed_mutation_response_is_ambiguous(string payload)
    {
        var transport = new Transport(r => Json(r.Path.EndsWith("connect/token") ? Response(r) : payload));
        await Assert.ThrowsAsync<CommerceProviderAmbiguousException>(() => Client(transport).AddGroupsAsync([], null, default));
        Assert.Single(transport.Requests, r => r.Path.EndsWith("shopping-cart-groups"));
    }

    [Fact]
    public async Task Invalid_delete_identifier_cannot_broaden_deletion()
    {
        var transport = new Transport(r => Json(Response(r))); var c = Client(transport);
        await Assert.ThrowsAsync<ArgumentException>(() => c.DeleteGroupAsync(Id, "", default));
        await Assert.ThrowsAsync<ArgumentException>(() => c.DeleteTicketAsync(Id, Guid.Empty.ToString(), default));
        Assert.Empty(transport.Requests);
    }

    [Theory]
    [InlineData("", "application/json", TouristPanelFailureKind.EmptyBody)]
    [InlineData("<html>gateway</html>", "text/html", TouristPanelFailureKind.Html)]
    [InlineData("{\"broken\":", "application/json", TouristPanelFailureKind.MalformedJson)]
    public async Task Read_response_shape_failures_are_classified_without_exposing_body(string payload, string contentType, TouristPanelFailureKind kind)
    {
        var transport = new Transport(r =>
        {
            if (r.Path.EndsWith("connect/token")) return Json(Response(r));
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(payload, Encoding.UTF8, contentType) };
        });
        var error = await Assert.ThrowsAsync<TouristPanelHttpException>(() => Client(transport).GetSettingsAsync(default));
        Assert.Equal(kind, error.Kind);
        if (payload.Length > 0) Assert.DoesNotContain(payload, error.Message);
    }
}
