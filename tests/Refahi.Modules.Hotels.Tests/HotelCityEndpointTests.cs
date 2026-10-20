using System.Net;
using System.Text.Json;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Refahi.Modules.Hotels.Api.EndPoints.Catalogue;
using Refahi.Modules.Hotels.Application.Contracts.Catalogue;
using Xunit;

namespace Refahi.Modules.Hotels.Tests;

public sealed class HotelCityEndpointTests
{
    [Theory]
    [InlineData("cityId=6433")]
    [InlineData("cityKey=11111111-1111-1111-1111-111111111111")]
    [InlineData("referenceCityId=1&cityId=6433")]
    [InlineData("referenceCityId=invalid")]
    [InlineData("referenceCityId=0")]
    public async Task LegacyOrInvalidCityParameters_ArePersianValidationErrors(string query)
    {
        await using var host = await Host.Start();
        using var response = await host.Http.GetAsync("/api/hotels/v2/availability/city?" + query);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Contains("لطفاً مقصد را دوباره انتخاب کنید", body.RootElement.GetProperty("errors").GetProperty("request")[0].GetString());
        Assert.Null(host.Sender.Query);
    }

    [Fact]
    public async Task ReferenceCityParameter_ReachesTheCanonicalSearchContract()
    {
        await using var host = await Host.Start();
        var parties = Uri.EscapeDataString("[{\"adults\":2,\"childrenAges\":[3]}]");
        using var response = await host.Http.GetAsync($"/api/hotels/v2/availability/city?referenceCityId=42&checkin=2030-01-01&checkout=2030-01-03&parties={parties}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var query = Assert.IsType<SearchHotelCatalogueQuery>(host.Sender.Query);
        Assert.Equal(42, query.ReferenceCityId); Assert.Equal([3], query.Travel.Rooms[0].ChildrenAges);
        Assert.Equal("no-store", response.Headers.CacheControl!.ToString());
    }

    [Fact]
    public async Task RequestStatusRequiresAuthentication_AndPassesOnlyAuthenticatedIdentity()
    {
        await using var host = await Host.Start(); var requestId = Guid.NewGuid();
        Assert.Equal(HttpStatusCode.Unauthorized, (await host.Http.GetAsync($"/api/hotels/v2/requests/{requestId}")).StatusCode);
        var user = Guid.NewGuid(); host.Http.DefaultRequestHeaders.Add("Test-User", user.ToString());
        var response = await host.Http.GetAsync($"/api/hotels/v2/requests/{requestId}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var query = Assert.IsType<GetHotelBookingStatusQuery>(host.Sender.Query);
        Assert.Equal(user, query.UserId); Assert.Equal(requestId, query.RequestId); Assert.False(query.IsAdmin);
        var body = await response.Content.ReadAsStringAsync(); Assert.DoesNotContain("Lamasoo", body); Assert.DoesNotContain("referenceId", body);
        Assert.Equal("no-store", response.Headers.CacheControl!.ToString());
        host.Http.DefaultRequestHeaders.Add("Test-Role", "Admin");
        await host.Http.GetAsync($"/api/hotels/v2/requests/{requestId}");
        Assert.True(Assert.IsType<GetHotelBookingStatusQuery>(host.Sender.Query).IsAdmin);
    }

    [Fact]
    public async Task UnknownCreate_ReturnsAcceptedWithDurableRequestIdentity()
    {
        await using var host = await Host.Start(); host.Http.DefaultRequestHeaders.Add("Test-User", Guid.NewGuid().ToString());
        var response = await host.Http.PostAsync("/api/hotels/v2/requests", new StringContent("""
            {"offerToken":"test","travel":{"checkIn":"2030-01-01","checkOut":"2030-01-03","rooms":[{"adults":1,"childrenAges":[]}]},"guests":[],"phone":"09123456789","expectedTotalPriceMinor":1000}
            """, System.Text.Encoding.UTF8, "application/json"));
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("NeedsReview", body.RootElement.GetProperty("data").GetProperty("bookingStatus").GetString());
        Assert.NotEqual(Guid.Empty, body.RootElement.GetProperty("data").GetProperty("requestId").GetGuid());
    }

    private sealed class TestAuth(Microsoft.Extensions.Options.IOptionsMonitor<Microsoft.AspNetCore.Authentication.AuthenticationSchemeOptions> options,
        ILoggerFactory logger, System.Text.Encodings.Web.UrlEncoder encoder)
        : Microsoft.AspNetCore.Authentication.AuthenticationHandler<Microsoft.AspNetCore.Authentication.AuthenticationSchemeOptions>(options, logger, encoder)
    {
        protected override Task<Microsoft.AspNetCore.Authentication.AuthenticateResult> HandleAuthenticateAsync()
        {
            if (!Request.Headers.TryGetValue("Test-User", out var user))
                return Task.FromResult(Microsoft.AspNetCore.Authentication.AuthenticateResult.NoResult());
            var identity = new System.Security.Claims.ClaimsIdentity([
                new(System.Security.Claims.ClaimTypes.NameIdentifier, user.ToString()),
                new(System.Security.Claims.ClaimTypes.Role, Request.Headers["Test-Role"].FirstOrDefault() ?? "User")], "Test");
            return Task.FromResult(Microsoft.AspNetCore.Authentication.AuthenticateResult.Success(
                new(new System.Security.Claims.ClaimsPrincipal(identity), "Test")));
        }
    }

    private sealed class Host(WebApplication app, HttpClient http, Sender sender) : IAsyncDisposable
    {
        public HttpClient Http => http;
        public Sender Sender => sender;
        public static async Task<Host> Start()
        {
            var builder = WebApplication.CreateBuilder(); builder.Logging.ClearProviders();
            builder.WebHost.ConfigureKestrel(options => options.Listen(IPAddress.Loopback, 0));
            var sender = new Sender(); builder.Services.AddSingleton<ISender>(sender); builder.Services.AddAuthorization(o => o.AddPolicy("UserOrAdmin", p => p.RequireAuthenticatedUser().RequireRole("User", "Admin")));
            builder.Services.AddAuthentication("Test").AddScheme<Microsoft.AspNetCore.Authentication.AuthenticationSchemeOptions, TestAuth>("Test", _ => { });
            var app = builder.Build(); new HotelCatalogueEndpoints().Map(app.MapGroup("/api/hotels"));
            await app.StartAsync();
            var address = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
            return new(app, new HttpClient { BaseAddress = new Uri(address) }, sender);
        }
        public async ValueTask DisposeAsync() { http.Dispose(); await app.DisposeAsync(); }
    }
    private sealed class Sender : ISender
    {
        public object? Query { get; private set; }
        public Task<T> Send<T>(IRequest<T> request, CancellationToken ct = default)
        {
            Query = request;
            object response = request switch
            {
                GetHotelBookingStatusQuery status => new HotelBookingStatusDto(status.RequestId, null, "Unpaid", "NeedsReview", false, DateTime.UtcNow, DateTime.UtcNow, null),
                CreateHotelCatalogueRequestCommand => new Refahi.Modules.Hotels.Application.Contracts.Services.HotelRequests.CreateHotelRequest.CreateHotelRequestResponse(Guid.NewGuid(), "Created", DateTime.UtcNow, 1000, "IRR", "NeedsReview", false),
                _ => new HotelSearchResponse([], false, null)
            };
            return Task.FromResult((T)response);
        }
        public Task<object?> Send(object request, CancellationToken ct = default) => throw new NotSupportedException();
        public IAsyncEnumerable<T> CreateStream<T>(IStreamRequest<T> request, CancellationToken ct = default) => throw new NotSupportedException();
        public IAsyncEnumerable<object?> CreateStream(object request, CancellationToken ct = default) => throw new NotSupportedException();
    }
}
