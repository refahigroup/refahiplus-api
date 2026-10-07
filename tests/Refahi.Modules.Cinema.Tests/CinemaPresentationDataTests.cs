using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Refahi.Modules.Cinema.Application.Contracts;
using Refahi.Modules.Cinema.Infrastructure.Providers.iTicket;
using Refahi.Modules.Cinema.Infrastructure.Providers.iTicket.Client;
using Xunit;

namespace Refahi.Modules.Cinema.Tests;

public sealed class CinemaPresentationDataTests
{
    [Fact]
    public void Html_preserves_formatting_and_removes_active_content()
    {
        var html = CinemaHtml.Sanitize("""<p onclick="bad()" style="color:red">داستان <strong>فیلم</strong><br>ادامه</p><ul><li>آیتم</li></ul><script>alert(1)</script><iframe src="https://evil.test"></iframe><form><input></form><a href="javascript:bad()">بد</a><a href="https://example.com">سالم</a>""");
        Assert.Contains("<strong>فیلم</strong>", html);
        Assert.Contains("<li>آیتم</li>", html);
        Assert.Contains("href=\"https://example.com\"", html);
        foreach (var forbidden in new[] { "onclick", "style=", "script", "iframe", "<form", "<input", "javascript:" }) Assert.DoesNotContain(forbidden, html);
        Assert.Equal("یک دو سه", CinemaHtml.PlainText("<p>یک</p><p>دو<br>سه</p>"));
    }

    [Fact]
    public void Legacy_snapshot_deserializes_without_new_fields()
    {
        var show = JsonSerializer.Deserialize<CinemaShow>("""{"id":"old","title":"قدیمی","kind":"cinema","artists":["هنرمند"]}""", new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Assert.Equal("قدیمی", show!.Title);
        Assert.Null(show.ArtistDetails); Assert.Null(show.DescriptionHtml);
        var place = JsonSerializer.Deserialize<CinemaPlace>("""{"id":"place","title":"سینما"}""", new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Assert.Null(place!.Cover);
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(3)]
    public async Task Landing_preserves_all_valid_banners_and_safe_links(int count)
    {
        var banners = Enumerable.Range(0, count).Select(i => new { attributes = new { title = $"Banner {i}", image = new { full = $"https://example.com/{i}.jpg" }, sort_order = count-i, link = new { url = i == 0 ? "javascript:bad()" : "https://example.com/show" } } });
        using var handler = new Handler(path => path.Contains("marketing/banners") ? JsonSerializer.Serialize(new { data = banners }) : "{\"data\":[]}");
        var landing = await Provider(handler).LandingAsync(null, default);
        Assert.Equal(count, landing.Banners.Count);
        if (count > 0) Assert.Null(landing.Banners.Single(b=>b.Title=="Banner 0").Url);
        if (count > 1) Assert.Equal($"Banner {count-1}", landing.Banners[0].Title);
    }

    [Fact]
    public async Task Show_maps_genres_safe_html_and_coalesces_portrait_reads()
    {
        using var handler = new Handler(path => path.Contains("show-artists")
            ? """{"data":{"id":"artist","attributes":{"name":"هنرمند","portrait":{"full":"https://example.com/portrait.jpg"}}}}"""
            : """{"data":{"id":"show","attributes":{"title":"فیلم","summary":"<p>خلاصه <b>داستان</b></p>","description":"<p onclick='bad()'>توضیح</p>","genres":[{"id":"drama","name":"درام"}],"artists":[{"id":"artist","name":"هنرمند"},{"id":"artist","name":"هنرمند دوم"}]}}}""");
        var provider = Provider(handler);
        var shows = await Task.WhenAll(Enumerable.Range(0, 3).Select(_=>provider.ShowAsync("show",default)));
        Assert.All(shows, show =>
        {
            Assert.Equal("درام", Assert.Single(show.Genres!).Name);
            Assert.All(show.ArtistDetails!, a=>Assert.Equal("https://example.com/portrait.jpg",a.Portrait));
            Assert.Equal("خلاصه داستان",show.SummaryText);
            Assert.DoesNotContain("onclick",show.DescriptionHtml!);
        });
        Assert.Equal(1,handler.Paths.Count(p=>p.Contains("show-artists")));
    }

    [Fact]
    public async Task Place_details_are_deduplicated_bounded_and_media_falls_back()
    {
        var places = Enumerable.Range(0, 9).Select(i=>new { id=i.ToString(), title="سینما" });
        var dates = new[] { new { date="2026-10-09", places }, new { date="2026-10-10", places } };
        using var handler = new Handler(path => path.Contains("schedules/shows")
            ? JsonSerializer.Serialize(new { data=new { attributes=new { dates } } })
            : """{"data":{"attributes":{"media":{"gallery":[{"full":"https://example.com/gallery.jpg"}],"logo":{"full":"https://example.com/logo.jpg"}}}}}""", delay:true);
        var days = await Provider(handler).PlacesAsync("show",null,null,default);
        Assert.All(days.SelectMany(d=>d.Places), p=>Assert.Equal("https://example.com/gallery.jpg",p.Cover));
        Assert.Equal(9,handler.Paths.Count(p=>!p.Contains("schedules/shows")));
        Assert.InRange(handler.MaxActive,1,4);
    }

    [Fact]
    public async Task Embedded_primary_cover_needs_no_detail_request()
    {
        using var handler=new Handler(_=>"""{"data":{"attributes":{"dates":[{"date":"2026-10-09","places":[{"id":"place","title":"سینما","media":{"primary_media":{"full":"https://example.com/cover.jpg"}}}]}]}}}""");
        var days=await Provider(handler).PlacesAsync("show",null,null,default);
        Assert.Equal("https://example.com/cover.jpg",days[0].Places[0].Cover);
        Assert.Single(handler.Paths);
    }

    [Fact]
    public async Task Optional_media_failure_does_not_hide_catalog()
    {
        using var handler = new Handler(path=>path.Contains("schedules/shows")
            ? """{"data":{"attributes":{"dates":[{"date":"2026-10-09","places":[{"id":"place","title":"سینما"}]}]}}}"""
            : null);
        var days=await Provider(handler).PlacesAsync("show",null,null,default);
        Assert.Equal("سینما",Assert.Single(Assert.Single(days).Places).Title);
        Assert.Null(days[0].Places[0].Cover);
    }

    private static ITicketCinemaProvider Provider(Handler handler) => new(
        new iTicketClient(new HttpClient(handler) { BaseAddress=new("https://console.iticket.ir/api/v1/") },Options.Create(new iTicketOptions { AccessToken="fixture" })),
        Options.Create(new CinemaProviderSettings()),Options.Create(new iTicketOptions { AccessToken="fixture" }),
        new MemoryCache(new MemoryCacheOptions()),NullLogger<ITicketCinemaProvider>.Instance);

    private sealed class Handler(Func<string,string?> response, bool delay=false) : HttpMessageHandler
    {
        public readonly System.Collections.Concurrent.ConcurrentBag<string> Paths=[];
        private int _active; public int MaxActive;
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken ct)
        {
            var path=request.RequestUri!.PathAndQuery; Paths.Add(path);
            var active=Interlocked.Increment(ref _active);
            lock(Paths) MaxActive=Math.Max(MaxActive,active);
            try
            {
                if(delay) await Task.Delay(30,ct);
                var json=response(path);
                return new(json is null ? HttpStatusCode.NotFound : HttpStatusCode.OK) { Content=new StringContent(json ?? "{}") };
            }
            finally { Interlocked.Decrement(ref _active); }
        }
    }
}
