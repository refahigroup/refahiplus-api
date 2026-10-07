using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Refahi.Modules.Cinema.Infrastructure.Providers.iTicket;
using Refahi.Modules.Cinema.Infrastructure.Providers.iTicket.Client;
using Refahi.Modules.Cinema.Infrastructure.Providers.iTicket.Dtos.ResellerSchedule;
using Refahi.Modules.Cinema.Application.Contracts;
using Xunit;
namespace Refahi.Modules.Cinema.Tests;
public sealed class CinemaProviderTests
{
    [Theory][InlineData("IRT",123,1230)][InlineData("IRR",123,123)]
    public void Money_normalizes_currency(string currency,long value,long expected)=>Assert.Equal(expected,ITicketCinemaProvider.Money(value,currency));
    [Fact]public void Unknown_currency_is_rejected()=>Assert.Throws<CinemaException>(()=>ITicketCinemaProvider.Money(1,"USD"));
    [Fact]public void Money_overflow_is_rejected()=>Assert.Throws<OverflowException>(()=>ITicketCinemaProvider.Money(long.MaxValue,"IRT"));
    [Fact]public async Task Http_client_preserves_api_prefix_and_unwraps_single_resource()
    {
        var handler=new StubHandler("""{"data":{"type":"orders","id":"provider-order","attributes":{"code":"ticket","total_amount":3000000000}}}""");
        var client=Client(handler);
        var response=await client.ShowResellerOrderAsync(new(){Order="provider-order"});
        Assert.Equal("/api/v1/reseller/orders/provider-order",handler.Uri!.AbsolutePath);
        Assert.Equal("provider-order",response.Id);Assert.Equal(3_000_000_000,response.Attributes.TotalAmount);
        Assert.True(handler.TokenPresent);
    }
    [Fact]public async Task Real_public_seat_map_preserves_geometry_prices_and_rows()
    {
        var json=await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory,"Fixtures","iticket-seat-map.json"));
        using var doc=JsonDocument.Parse(json);var id=doc.RootElement.GetProperty("data").GetProperty("id").GetString()!;
        var provider=Provider(new StubHandler(json));var map=await provider.SeatsAsync(id,default);
        Assert.NotEmpty(map.Show.Title);Assert.NotEmpty(map.Place.Title);Assert.Equal(49,map.Seats.Count);
        Assert.Equal("01ktm9ahefk1ntpw59hh60w65y",map.HallId);
        Assert.All(map.Seats,s=>Assert.Equal(1_800_000,s.PriceMinor));
        Assert.Equal("1",map.Seats[0].Row);Assert.Equal(210,map.Seats[0].Y);Assert.Equal(0,map.Seats[0].X);
        Assert.All(map.Seats,s=>Assert.Equal(26,s.Id.Length));
    }
    [Fact]public async Task Unknown_status_index_is_blocked()
    {
        var p=Provider(new StubHandler("""{"data":{"attributes":{"schema":{"status":["available","reserved","sold","blocked"]},"seats":{"a":99,"b":1,"c":null}}}}"""));
        var result=await p.StatusAsync("schedule",default);Assert.Equal("blocked",result.Seats["a"]);Assert.Equal("reserved",result.Seats["b"]);Assert.Equal("blocked",result.Seats["c"]);
    }
    [Fact]public async Task Mutating_transport_timeout_is_ambiguous_and_not_retried()
    {
        var handler=new StubHandler("",true);var p=Provider(handler);
        await Assert.ThrowsAsync<CinemaProviderAmbiguousException>(()=>p.ConfirmAsync("order",default));Assert.Equal(1,handler.Calls);
    }
    [Fact]public async Task Malformed_success_preserves_known_provider_reference_for_reconciliation()
    {
        var handler=new StubHandler("""{"data":{"id":"known-provider-order","attributes":{"currency_code":"USD","status":"reserved"}}}""");
        var error=await Assert.ThrowsAsync<CinemaProviderAmbiguousException>(()=>Provider(handler).ReserveAsync("schedule",["seat"],new("09123456789","آزمایشی"),default));
        Assert.Equal("known-provider-order",error.ProviderOrderId);Assert.Equal(1,handler.Calls);
    }
    [Fact]
    public async Task Landing_reads_configured_sections_and_maps_provider_resources()
    {
        var settings = new CinemaProviderSettings
        {
            CinemaCategories = ["cinema-category"], TheaterCategories = ["theater-category"],
            ArtCategories = ["art-category"]
        };
        var handler = new LandingHandler();
        var provider = new ITicketCinemaProvider(new iTicketClient(
            new HttpClient(handler) { BaseAddress = new("https://console.iticket.ir/api/v1/") },
            Options.Create(new iTicketOptions { AccessToken = "test-token" })),
            Options.Create(settings), Options.Create(new iTicketOptions()),
            new MemoryCache(new MemoryCacheOptions()), NullLogger<ITicketCinemaProvider>.Instance);
        var landing = await provider.LandingAsync(12, default);
        Assert.Single(landing.Banners);
        Assert.Equal("https://example.com/banner.jpg", landing.Banners[0].ImageUrl);
        Assert.Equal("cinema", Assert.Single(landing.Cinema).Kind);
        Assert.Equal("theater", Assert.Single(landing.Theater).Kind);
        Assert.Equal("art", Assert.Single(landing.Art).Kind);
        Assert.Equal(1230L, Assert.Single(landing.Rankings).AmountMinor);
        foreach (var category in new[] { "cinema-category", "theater-category", "art-category" })
            Assert.Contains(handler.Paths, path => path.Contains("category[]=" + category) &&
                path.Contains("city[]=12") && path.Contains("only_with_active_sessions=true"));
        Assert.Contains(handler.Paths, path => path.Contains("placement=home-1"));
    }
    private sealed class LandingHandler : HttpMessageHandler
    {
        public List<string> Paths { get; } = [];
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var path = Uri.UnescapeDataString(request.RequestUri!.PathAndQuery);
            Paths.Add(path);
            var json = path.Contains("marketing/banners")
                ? """{"data":[{"attributes":{"title":"Banner","image":{"full":"https://example.com/banner.jpg"}}}]}"""
                : path.Contains("box-office")
                    ? """{"data":[{"show_id":"show","title":"Movie","final_price":123}]}"""
                    : """{"data":[{"id":"show","attributes":{"title":"Show","media":{"poster":{"full":"https://example.com/poster.jpg"}}}}],"meta":{"last_page":2}}""";
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json) });
        }
    }
    private static iTicketClient Client(StubHandler h)=>new(new HttpClient(h){BaseAddress=new("https://console.iticket.ir/api/v1/")},Options.Create(new iTicketOptions{AccessToken="test-token"}));
    private static ITicketCinemaProvider Provider(StubHandler h)=>new(Client(h),Options.Create(new CinemaProviderSettings()),Options.Create(new iTicketOptions()),new MemoryCache(new MemoryCacheOptions()),NullLogger<ITicketCinemaProvider>.Instance);
    private sealed class StubHandler(string json,bool timeout=false) : HttpMessageHandler
    {
        public Uri? Uri;public bool TokenPresent;public int Calls;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken ct)
        {Calls++;Uri=request.RequestUri;TokenPresent=request.Headers.Contains("X-Api-Access-Token");if(timeout)throw new HttpRequestException("connection lost");return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK){Content=new StringContent(json)});}
    }
}
