using System.Net;
using System.Text;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Refahi.Modules.Pools.Application;
using Refahi.Modules.Pools.Application.Validators;
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
    [Theory]
    [InlineData("1", true)] [InlineData("0", false)]
    [InlineData("true", true)] [InlineData("false", false)]
    public async Task Search_accepts_numeric_and_boolean_sale_flags(string flag, bool expected)
    {
        var json = """{"page":1,"limit":50,"status":1,"pools":[{"pool_id":2,"name":"استخر ارمغان سپید ایرانیان مشهد","lat":36.297577614645,"lng":59.60572242737,"status_sell":FLAG,"status_sell_course":1,"status_sell_pack":1,"gender":2,"comment":1,"state_name":"آذربایجان شرقی","city_name":"تبریز","area_name":"محله تبریزی ها","state_id":1,"city_id":1,"area_id":2,"default_image":"https://staging.poolticket.org/files/images/pg/35/270_160_2.webp","max_discount":30,"rating":3.3,"minimum_sale_price":700,"minimum_market_price":1000,"type_id":1,"status_sell_ticket":1}],"sort_list":[{"title":"حروف الفبا","code":"name"}],"sorted_by":"name"}""".Replace("FLAG", flag);
        var catalog = await Provider(new(json)).SearchAsync(new("poolticket", Page: 1, Limit: 50), default);
        var pool = Assert.Single(catalog.Items);
        Assert.Equal(expected, pool.IsSelling);
        Assert.Equal(700m, pool.MinimumSalePrice);
        Assert.Equal("name", catalog.SortedBy);
    }

    [Theory]
    [InlineData("1", true)] [InlineData("0", false)]
    [InlineData("true", true)] [InlineData("false", false)]
    public async Task Sessions_accept_numeric_and_boolean_sale_flags(string flag, bool expected)
    {
        var json = """{"status":1,"saens":[{"saens_id":7,"saens_name":"روز","gender":2,"rows":[{"saens_row_id":9,"status_sell":FLAG,"adult_sale_status":FLAG,"child_sale_status":FLAG,"price_sale_adult":100,"price_sale_child":200,"price_market_adult":120,"price_market_child":220,"start_date":"1405/07/20","start_day":"روز","time":"10:00"}]}]}""".Replace("FLAG", flag);
        var sessions = await Provider(new(json)).SessionsAsync("2", default);
        var row = Assert.Single(Assert.Single(sessions).Rows);
        Assert.Equal(expected, row.IsSelling);
        Assert.Equal(expected, row.AdultEnabled);
        Assert.Equal(expected, row.ChildEnabled);
    }

    [Theory]
    [InlineData("2")] [InlineData("-1")] [InlineData("1.5")]
    [InlineData("\"1\"")] [InlineData("null")]
    public async Task Invalid_sale_flags_are_rejected(string flag)
    {
        var json = """{"status":1,"pools":[{"pool_id":2,"name":"استخر","status_sell":FLAG,"gender":2}]}""".Replace("FLAG", flag);
        var error = await Assert.ThrowsAsync<PoolException>(() => Provider(new(json)).SearchAsync(new("poolticket"), default));
        Assert.Equal(502, error.Status);
    }

    [Fact]
    public async Task Terms_accepts_provider_response_with_escaped_unicode_and_slash()
    {
        var handler = new StubHandler("""{"status":1,"terms_of_use":{"content":"<p>\u0634\u0631\u0627\u06cc\u0637 \u0627\u0633\u062a\u0641\u0627\u062f\u0647<\/p>"}}""");
        Assert.Equal("<p>شرایط استفاده</p>", await Provider(handler).TermsAsync("72", default));
        Assert.Equal("/api/v2/pool/terms-of-use/72", handler.Path);
    }

    [Theory]
    [InlineData("null")]
    [InlineData("\"\"")]
    public async Task Terms_without_content_returns_empty_text(string content)
    {
        var json = """{"status":1,"terms_of_use":{"content":CONTENT}}""".Replace("CONTENT", content);
        Assert.Equal("", await Provider(new(json)).TermsAsync("72", default));
    }

    [Theory]
    [InlineData(200)]
    [InlineData(201)]
    public async Task Terms_accepts_reported_pool_88_response(int httpStatus)
    {
        var json = """{"status":1,"terms_of_use":{"content":"<p>۲۰ دقیقه پایانی سانس به دوش&zwnj;گرفتن و تعویض لباس اختصاص دارد.</p>\r\n<p>پذیرش کودکان 5 سال به بالا امکان پذیر است.</p>\r\n<p>سانس صبحگاهی همراه با پذیرایی صبحانه است.</p>\r\n<p>سانس vip همراه با دیجی است.</p>\r\n<p>&nbsp;</p>"}}""";
        var handler = new StubHandler(json, (HttpStatusCode)httpStatus);
        var terms = await Provider(handler).TermsAsync("88", default);
        Assert.Contains("۲۰ دقیقه پایانی سانس", terms);
        Assert.Contains("پذیرش کودکان 5 سال به بالا", terms);
        Assert.Contains("سانس vip همراه با دیجی است.", terms);
        Assert.Equal("/api/v2/pool/terms-of-use/88", handler.Path);
    }

    [Theory]
    [InlineData(404)]
    [InlineData(500)]
    public async Task Terms_rejects_unsuccessful_http_even_with_successful_envelope(int httpStatus)
    {
        var handler = new StubHandler("""{"status":1,"terms_of_use":{"content":"<p>قانون</p>"}}""", (HttpStatusCode)httpStatus);
        var error = await Assert.ThrowsAsync<PoolException>(() => Provider(handler).TermsAsync("88", default));
        Assert.Equal(502, error.Status);
    }

    [Theory]
    [InlineData("{\"status\":1}")]
    [InlineData("{\"status\":1,\"terms_of_use\":null}")]
    public async Task Terms_without_envelope_is_rejected(string json)
    {
        var error = await Assert.ThrowsAsync<PoolException>(() => Provider(new(json)).TermsAsync("72", default));
        Assert.Equal(502, error.Status);
    }

    [Fact]
    public async Task Sessions_requests_json_and_preserves_api_errors()
    {
        var handler = new StubHandler("""{"status":0,"error":"invalid_pool"}""");
        var error = await Assert.ThrowsAsync<PoolException>(() => Provider(handler).SessionsAsync("72", default));
        Assert.Equal(404, error.Status);
        Assert.Equal("/api/v2/saens/list", handler.Path);
        Assert.Equal("application/json", handler.Accept);
        Assert.Contains("pool_id=72", handler.Body);
        Assert.Contains("client_id=test-client", handler.Body);
    }

    [Fact]
    public void Registered_provider_client_does_not_follow_redirects()
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStringName"] = "Test",
            ["ConnectionStrings:Test"] = "Host=localhost;Database=pools_test;Username=test;Password=test"
        }).Build();
        var services = new ServiceCollection();
        services.AddLogging();
        Refahi.Modules.Pools.Infrastructure.DI.RegisterInfrastructure(services, config);
        using var scope = services.BuildServiceProvider();
        var handler = scope.GetRequiredService<IHttpMessageHandlerFactory>().CreateHandler(nameof(PoolTicketClient));
        while (handler is DelegatingHandler wrapper)
            handler = wrapper.InnerHandler!;
        Assert.False(Assert.IsType<HttpClientHandler>(handler).AllowAutoRedirect);
    }

    [Theory]
    [InlineData(200, "text/html", "POOLS_PROVIDER_NON_JSON")]
    [InlineData(502, "text/html", "POOLS_PROVIDER_NON_JSON")]
    [InlineData(302, "text/html", "POOLS_PROVIDER_REDIRECT")]
    [InlineData(307, "text/html", "POOLS_PROVIDER_REDIRECT")]
    public async Task Sessions_rejects_website_and_redirect_responses(int status, string mediaType, string code)
    {
        var handler = new StubHandler("<!DOCTYPE html><html><meta name='csrf-token' content='private-token'></html>", (HttpStatusCode)status, mediaType);
        var error = await Assert.ThrowsAsync<PoolException>(() => Provider(handler).SessionsAsync("72", default));
        Assert.Equal(502, error.Status);
        Assert.Equal(code, error.Code);
        Assert.DoesNotContain("private-token", error.Message);
    }

    [Fact]
    public async Task Success_http_with_status_zero_is_rejected()
    {
        var p = Provider(new("""{"status":0,"error":"invalid_client"}"""));
        var ex = await Assert.ThrowsAsync<PoolException>(() => p.StatesAsync(true, default)); Assert.Equal(502, ex.Status);
    }

    [Theory]
    [InlineData("{\"status\":0}", "application/json")]
    [InlineData("{\"status\":1,\"terms_of_use\":{\"content\":\"text\"}}", "application/json")]
    [InlineData("<html>Too many requests</html>", "text/html")]
    [InlineData("", "text/plain")]
    public async Task Rate_limit_is_reported_independently_of_response_body(string body, string mediaType)
    {
        var handler = new StubHandler(body, HttpStatusCode.TooManyRequests, mediaType);
        var error = await Assert.ThrowsAsync<PoolException>(() => Provider(handler).TermsAsync("88", default));
        Assert.Equal(429, error.Status);
        Assert.Equal("POOLS_PROVIDER_RATE_LIMITED", error.Code);
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
    private static PoolTicketProvider Provider(StubHandler handler)
    {
        var options = Options.Create(new PoolTicketOptions { ClientId = "test-client", CalculateReadOnlyConfirmed = true });
        return new(new(new HttpClient(handler) { BaseAddress = new("https://example.test/") }, options, NullLogger<PoolTicketClient>.Instance), options);
    }
    private sealed class StubHandler(string json, HttpStatusCode status = HttpStatusCode.OK, string mediaType = "application/json") : HttpMessageHandler
    {
        public string? Body, Path, Accept;
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage r, CancellationToken ct)
        { Body = await r.Content!.ReadAsStringAsync(ct); Path = r.RequestUri!.AbsolutePath; Accept = r.Headers.Accept.ToString(); return new(status) { Content = new StringContent(json, Encoding.UTF8, mediaType) }; }
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

