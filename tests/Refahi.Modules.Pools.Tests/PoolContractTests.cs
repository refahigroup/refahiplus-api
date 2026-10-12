using System.Net;
using System.Text;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Refahi.Modules.Pools.Application;
using Refahi.Modules.Pools.Application.Contracts;
using Refahi.Modules.Pools.Domain;
using Refahi.Modules.Pools.Infrastructure.Providers;
using Refahi.Modules.Pools.Infrastructure.Providers.PoolTicket;
using Xunit;

namespace Refahi.Modules.Pools.Tests;
public sealed class PoolContractTests
{
    [Theory]
    [InlineData("IRR", 100, 100)] [InlineData("TOMAN", 100, 1000)]
    public void Money_uses_only_confirmed_units(string unit, decimal amount, long expected) => Assert.Equal(expected, PoolTicketProvider.ToMinor(amount, unit));
    [Fact]
    public void Unknown_unit_never_creates_payable_money()
    {
        Assert.Null(PoolTicketProvider.ToMinor(100, null));
        Assert.Throws<PoolException>(() => PoolTicketProvider.ToMinor(1.1m, "IRR"));
        Assert.Throws<PoolException>(() => PoolTicketProvider.ToMinor(decimal.MaxValue, "TOMAN"));
    }
    [Theory]
    [InlineData("invalid_client", 502)] [InlineData("invalid_state", 400)] [InlineData("invalid_pool", 404)]
    [InlineData("invalid_saens_row", 404)] [InlineData("pool_capacity_overflow", 409)]
    [InlineData("not_available_ticket_serial", 409)] [InlineData("coupon_error", 400)] [InlineData("unknown_secret", 502)]
    public void Provider_errors_are_safe_and_do_not_log_out_the_user(string error, int status)
    { var mapped = PoolTicketClient.MapError(error); Assert.Equal(status, mapped.Status); Assert.DoesNotContain(error, mapped.Message); }
    [Fact]
    public async Task Search_maps_nullable_prices_sorts_and_server_client_id()
    {
        var handler = new StubHandler("""{"status":1,"page":1,"limit":10,"pools":[{"pool_id":12,"name":"استخر","status_sell":true,"gender":2,"state_name":"تهران","city_name":"تهران","minimum_sale_price":null}],"sort_list":[{"code":"name","title":"نام"}],"sorted_by":"name"}""");
        var p = Provider(handler);
        var catalog = await p.SearchAsync(new("poolticket", CityId: "3"), default);
        Assert.Equal("12", catalog.Items.Single().Id); Assert.Null(catalog.Items.Single().MinimumSalePrice);
        Assert.Equal("name", catalog.Sorts.Single().Code); Assert.Contains("client_id=test-client", handler.Body);
        Assert.Contains("city_id=3", handler.Body); Assert.DoesNotContain("access_token", handler.Body);
        Assert.Equal("/api/v2/pools/1/10", handler.Path);
    }
    [Fact]
    public async Task Success_http_with_status_zero_is_rejected()
    {
        var p = Provider(new("""{"status":0,"error":"invalid_client"}"""));
        var ex = await Assert.ThrowsAsync<PoolException>(() => p.StatesAsync(true, default)); Assert.Equal(502, ex.Status);
    }
    [Fact]
    public async Task Malformed_or_missing_provider_fields_are_rejected()
    {
        foreach (var json in new[] { "{invalid", "{\"status\":1}", "{\"status\":1,\"states\":[null]}", "{\"status\":1,\"states\":[{\"state_id\":0,\"name\":\"x\"}]}" })
            Assert.Equal(502, (await Assert.ThrowsAsync<PoolException>(() => Provider(new(json)).StatesAsync(true, default))).Status);
    }
    [Fact]
    public void Html_rejects_scripts_handlers_and_unsafe_links()
    {
        var html = PoolTicketProvider.Html("<p onclick='alert(1)'>قوانین</p><script>alert(1)</script><a href='javascript:alert(1)'>link</a><iframe src='x'></iframe>", "html");
        Assert.DoesNotContain("script", html); Assert.DoesNotContain("onclick", html); Assert.DoesNotContain("iframe", html);
        Assert.Equal("&lt;b&gt;text&lt;/b&gt;", PoolTicketProvider.Html("<b>text</b>", "text"));
    }
    [Fact]
    public async Task Cities_sessions_terms_and_calculate_follow_documented_wire_contracts()
    {
        var options = Options.Create(new PoolTicketOptions { ClientId = "test-client", PriceUnit = "IRR", CalculateReadOnlyConfirmed = true });
        var handler = new RoutingHandler();
        var p = new PoolTicketProvider(new(new HttpClient(handler) { BaseAddress = new("https://example.test/") }, options, NullLogger<PoolTicketClient>.Instance), options);
        Assert.Equal("3", (await p.CitiesAsync("2", true, default)).Single().Id);
        var row = (await p.SessionsAsync("12", default)).Single().Rows.Single(); Assert.Equal("9", row.Id); Assert.Equal(200, row.ChildPrice);
        Assert.Equal("<p>قانون</p>", await p.TermsAsync("12", default));
        var quote = await p.QuoteAsync(new("poolticket", "12", "9", 1, 2), default); Assert.Equal(500, quote.TotalMinor);
        Assert.Contains("state_id=2", handler.Bodies["cities"]); Assert.Contains("pool_id=12", handler.Bodies["saens/list"]);
        Assert.Contains("saens_row_id=9", handler.Bodies["reservation/calculate"]); Assert.Contains("count_child=2", handler.Bodies["reservation/calculate"]);
        Assert.DoesNotContain("coupon", handler.Bodies["reservation/calculate"]);
        options.Value.CalculateReadOnlyConfirmed = false;
        Assert.False(p.Capabilities.Quote); Assert.Equal(503, (await Assert.ThrowsAsync<PoolException>(() => p.QuoteAsync(new("poolticket", "12", "9", 1, 2), default))).Status);
    }
    [Fact]
    public async Task Timeout_network_and_request_cancellation_remain_distinct()
    {
        var options = Options.Create(new PoolTicketOptions { ClientId = "test-client" });
        PoolTicketProvider Create(Exception error) => new(new(new HttpClient(new ThrowingHandler(error)) { BaseAddress = new("https://example.test/") }, options, NullLogger<PoolTicketClient>.Instance), options);
        Assert.Equal(504, (await Assert.ThrowsAsync<PoolException>(() => Create(new TaskCanceledException()).StatesAsync(true, default))).Status);
        Assert.Equal(502, (await Assert.ThrowsAsync<PoolException>(() => Create(new HttpRequestException()).StatesAsync(true, default))).Status);
        using var stop = new CancellationTokenSource(); stop.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Create(new TaskCanceledException()).StatesAsync(true, stop.Token));
    }
    [Fact]
    public void Validation_covers_location_and_quantities()
    {
        var search = new PoolSearchValidator();
        Assert.False(search.Validate(new PoolSearchQuery("poolticket", CityId: "1", Latitude: 1, Longitude: 1)).IsValid);
        Assert.False(search.Validate(new PoolSearchQuery("poolticket", Latitude: 1)).IsValid);
        Assert.False(search.Validate(new PoolSearchQuery("poolticket", Gender: 2)).IsValid);
        Assert.False(search.Validate(new PoolSearchQuery("poolticket", Page: 0, Limit: 51)).IsValid);
        var selection = new PoolSelectionValidator();
        Assert.False(selection.Validate(new PoolSelection("poolticket", "1", "1", 0, 0)).IsValid);
        Assert.True(selection.Validate(new PoolSelection("poolticket", "1", "1", 0, 1)).IsValid);
        Assert.All(selection.Validate(new PoolSelection("", "", "", -1, 101)).Errors, e => Assert.Matches("[\u0600-\u06ff]", e.ErrorMessage));
        Assert.All(search.Validate(new PoolSearchQuery("", Page: 0, Limit: 51)).Errors, e => Assert.Matches("[\u0600-\u06ff]", e.ErrorMessage));
    }
    [Fact]
    public void Production_policy_rejects_mock_even_with_purchase_flags()
    {
        var options = Options.Create(new PoolsOptions { PurchaseEnabled = true, MockLifecycleEnabled = true, TestFundsConfirmed = true });
        Assert.Throws<PoolException>(() => new PoolPurchasePolicy(new TestEnvironment("Production"), options).EnsureAllowed(true));
        new PoolPurchasePolicy(new TestEnvironment("Staging"), options).EnsureAllowed(true);
    }
    [Fact]
    public void Mock_requires_explicit_confirmation_of_isolated_test_funds_and_cannot_enable_live_purchase()
    {
        var options = Options.Create(new PoolsOptions { PurchaseEnabled = true, MockLifecycleEnabled = true });
        var policy = new PoolPurchasePolicy(new TestEnvironment("Test"), options);
        Assert.Throws<PoolException>(() => policy.EnsureAllowed(true));
        options.Value.TestFundsConfirmed = true;
        Assert.Throws<PoolException>(() => policy.EnsureAllowed(false));
    }
    [Fact]
    public void Domain_never_issues_before_payment_or_after_cancel()
    {
        var b = PoolBooking.Create(Guid.NewGuid(), "poolticket", "key", "hash", "{}", true);
        b.Reserve("MOCK-1", 100, DateTimeOffset.UtcNow.AddMinutes(10));
        Assert.Throws<InvalidOperationException>(() => b.Issue("MOCK-code"));
        b.MarkPaid(); b.Cancel(); Assert.Throws<InvalidOperationException>(() => b.Issue("MOCK-code"));
    }
    private static PoolTicketProvider Provider(StubHandler handler)
    {
        var options = Options.Create(new PoolTicketOptions { ClientId = "test-client", CalculateReadOnlyConfirmed = true });
        return new(new(new HttpClient(handler) { BaseAddress = new("https://example.test/") }, options, NullLogger<PoolTicketClient>.Instance), options);
    }
    private sealed class StubHandler(string json) : HttpMessageHandler
    {
        public string? Body, Path;
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage r, CancellationToken ct)
        { Body = await r.Content!.ReadAsStringAsync(ct); Path = r.RequestUri!.AbsolutePath; return new(HttpStatusCode.OK) { Content = new StringContent(json, Encoding.UTF8, "application/json") }; }
    }
    private sealed class ThrowingHandler(Exception error) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage r, CancellationToken ct) => Task.FromException<HttpResponseMessage>(error);
    }
    private sealed class RoutingHandler : HttpMessageHandler
    {
        public readonly Dictionary<string, string> Bodies = [];
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage r, CancellationToken ct)
        {
            var path = r.RequestUri!.AbsolutePath["/api/v2/".Length..]; Bodies[path] = await r.Content!.ReadAsStringAsync(ct);
            var json = path switch
            {
                "cities" => """{"status":1,"cities":[{"state_id":2,"state_name":"استان","city_id":3,"city_name":"شهر"}]}""",
                "saens/list" => """{"status":1,"saens":[{"saens_id":7,"saens_name":"روز","gender":2,"rows":[{"saens_row_id":9,"status_sell":true,"adult_sale_status":true,"child_sale_status":true,"price_sale_adult":100,"price_sale_child":200,"price_market_adult":120,"price_market_child":220,"start_date":"1405/07/20","start_day":"روز","time":"10:00"}]}]}""",
                "pool/terms-of-use/12" => """{"status":1,"terms_of_use":{"content":"<p>قانون</p>"}}""",
                "reservation/calculate" => """{"status":1,"cart":{"saens_row_id":9,"total_sale":500,"total_market":560,"count_adult":1,"count_child":2,"start_date":"1405/07/20","start_day":"روز","time":"10:00"}}""",
                _ => throw new InvalidOperationException(path)
            };
            return new(HttpStatusCode.OK) { Content = new StringContent(json, Encoding.UTF8, "application/json") };
        }
    }
}
public sealed class TestEnvironment(string name) : IHostEnvironment
{
    public string EnvironmentName { get; set; } = name;
    public string ApplicationName { get; set; } = "Pools.Tests";
    public string ContentRootPath { get; set; } = Directory.GetCurrentDirectory();
    public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } = new Microsoft.Extensions.FileProviders.NullFileProvider();
}

