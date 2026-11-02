using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using MediatR;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Refahi.Modules.Promotions.Application.Contracts;
using Refahi.Modules.Orders.Domain.Repositories;
using Refahi.Modules.Orders.Domain.Aggregates;
using Xunit;
namespace Refahi.Modules.Promotions.Tests;

[Collection("Promotions database")]
public sealed class PromotionApiTests(PromotionDatabase database)
{
    [PostgresFact]
    public async Task AdminRoutes_EnforceRoles_AndUseUnifiedMaskedResponses()
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Testing" });
        builder.Logging.SetMinimumLevel(LogLevel.Warning);
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStringName"] = "Test",
            ["ConnectionStrings:Test"] = database.Connection,
            ["Promotions:Enabled"] = "true",
            ["Promotions:FundingWalletId"] = database.FundingWallet.ToString()
        });
        builder.Services.AddAuthentication("Test").AddScheme<AuthenticationSchemeOptions, TestAuthentication>("Test", _ => { });
        builder.Services.AddAuthorization(o => { o.AddPolicy("AdminOnly", p => p.RequireRole("Admin")); o.AddPolicy("UserOrAdmin", p => p.RequireRole("User", "Admin")); });
        Refahi.Modules.Promotions.Api.DI.RegisterPromotionsModule(builder.Services, builder.Configuration);
        Refahi.Modules.Organizations.Api.DI.RegisterOrganizationsModule(builder.Services, builder.Configuration);
        Refahi.Modules.Orders.Api.DI.RegisterOrdersModule(builder.Services, builder.Configuration);
        Refahi.Modules.Wallets.Api.DI.RegisterWalletsModule(builder.Services, builder.Configuration);
        builder.Services.AddTransient(typeof(IPipelineBehavior<,>), typeof(Refahi.Api.ValidationBehavior<,>));
        await using var app = builder.Build();
        app.UseMiddleware<Refahi.Api.Middlewares.ApiExceptionMiddleware>();
        app.UseRouting(); app.UseAuthentication(); app.UseRateLimiter(); app.UseAuthorization();
        new Refahi.Modules.Promotions.Api.PromotionEndpoints().Map(app.MapGroup("/api/promotions"));
        new Refahi.Modules.Organizations.Api.OrganizationEndpoints().Map(app.MapGroup("/api/organizations"));
        new Refahi.Modules.Orders.Api.Endpoints.OrderPromotionEndpoints().Map(app.MapGroup("/api/orders"));
        await app.StartAsync();
        try
        {
            using var client = new HttpClient { BaseAddress = new Uri(app.Urls.Single()) };
            Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/promotions/admin/campaigns")).StatusCode);
            client.DefaultRequestHeaders.Add("X-Test-Role", "User");
            Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/promotions/admin/campaigns")).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/organizations/admin/")).StatusCode);
            client.DefaultRequestHeaders.Remove("X-Test-Role"); client.DefaultRequestHeaders.Add("X-Test-Role", "Admin");
            using var funding=await client.GetAsync("/api/promotions/admin/funding");funding.EnsureSuccessStatusCode();
            var fundingStatus=await funding.Content.ReadFromJsonAsync<Envelope<Refahi.Modules.Orders.Application.Contracts.Queries.PromotionFundingStatusDto>>();
            Assert.True(fundingStatus!.Data!.IsValid);Assert.Equal(database.FundingWallet,fundingStatus.Data.WalletId);
            var rules = new CampaignRules("کمپین HTTP", "Fixed", 100, 0, 100, 0, 10000, 100, 1, [], [], [], [], DateTimeOffset.UtcNow.AddHours(-1), DateTimeOffset.UtcNow.AddHours(1));
            using var created = await client.PostAsJsonAsync("/api/promotions/admin/campaigns", rules);
            created.EnsureSuccessStatusCode(); var envelope = await created.Content.ReadFromJsonAsync<Envelope<CampaignDto>>();
            Assert.True(envelope!.Success); var id = envelope.Data!.Id;
            var publicCode = "API" + Guid.NewGuid().ToString("N").ToUpperInvariant();
            using var generated = await client.PostAsJsonAsync($"/api/promotions/admin/campaigns/{id}/codes", new { PublicCode = publicCode, UserId = (Guid?)null, Count = 1, MaximumUses = 10, IdempotencyKey = "http-batch" });
            generated.EnsureSuccessStatusCode(); var codes = await generated.Content.ReadFromJsonAsync<Envelope<List<CodeDto>>>();
            Assert.DoesNotContain(publicCode, await generated.Content.ReadAsStringAsync());
            using var export = await client.PostAsJsonAsync($"/api/promotions/admin/campaigns/{id}/codes/export", new { BatchKey = codes!.Data![0].BatchKey });
            export.EnsureSuccessStatusCode(); Assert.True(export.Headers.CacheControl!.NoStore);
            Assert.Contains(publicCode, await export.Content.ReadAsStringAsync());
            using var activated = await client.PutAsJsonAsync($"/api/promotions/admin/campaigns/{id}/active", new { Active = true }); activated.EnsureSuccessStatusCode();
            var user = Guid.Parse("11111111-1111-1111-1111-111111111111");
            var order = Order.Create(user, "Charge", Guid.NewGuid(), Guid.NewGuid().ToString("N"), "ChargeRequest", [new OrderItemData("تست", 1000, 1, 0, Guid.NewGuid(), "charge", null, null)]);
            using (var scope = database.Services.CreateScope()) await scope.ServiceProvider.GetRequiredService<IOrderRepository>().AddAsync(order);
            client.DefaultRequestHeaders.Remove("X-Test-Role"); client.DefaultRequestHeaders.Add("X-Test-Role", "User");
            using var apply = await client.PutAsJsonAsync($"/api/orders/{order.Id}/promotion", new { Code = publicCode, ExpectedPricingVersion = 1, IdempotencyKey = "apply-http" });
            apply.EnsureSuccessStatusCode(); var result = await apply.Content.ReadFromJsonAsync<Envelope<Refahi.Modules.Orders.Application.Contracts.Commands.AppliedOrderPromotionDto>>(); Assert.Equal(900, result!.Data!.FinalAmountMinor);
            Assert.DoesNotContain(database.FundingWallet.ToString(), await apply.Content.ReadAsStringAsync());
            using var stale = await client.PutAsJsonAsync($"/api/orders/{order.Id}/promotion", new { Code = publicCode, ExpectedPricingVersion = 1, IdempotencyKey = "apply-stale" }); Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
            var foreign = Order.Create(Guid.NewGuid(), "Charge", Guid.NewGuid(), Guid.NewGuid().ToString("N"), "ChargeRequest", [new OrderItemData("تست", 1000, 1, 0, Guid.NewGuid(), "charge", null, null)]);
            using (var scope = database.Services.CreateScope()) await scope.ServiceProvider.GetRequiredService<IOrderRepository>().AddAsync(foreign);
            using var denied = await client.PutAsJsonAsync($"/api/orders/{foreign.Id}/promotion", new { Code = publicCode, ExpectedPricingVersion = 1, IdempotencyKey = "apply-foreign" }); Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
            using var removeRequest=new HttpRequestMessage(HttpMethod.Delete,$"/api/orders/{order.Id}/promotion")
            {Content=JsonContent.Create(new{ExpectedPricingVersion=2,IdempotencyKey="remove-http"})};
            using var removed=await client.SendAsync(removeRequest);removed.EnsureSuccessStatusCode();
            var restored=await removed.Content.ReadFromJsonAsync<Envelope<Refahi.Modules.Orders.Application.Contracts.Commands.AppliedOrderPromotionDto>>();
            Assert.Equal(1000,restored!.Data!.FinalAmountMinor);Assert.Equal(3,restored.Data.PricingVersion);
            var limited = false;
            for (var i = 0; i < 25; i++)
            { using var attempt = await client.PostAsJsonAsync($"/api/orders/{order.Id}/promotion/preview", new { Code = "ABSENT", ExpectedPricingVersion = 3 }); limited |= attempt.StatusCode == HttpStatusCode.TooManyRequests; }
            Assert.True(limited);
        }
        finally { await app.StopAsync(); }
    }
    private sealed record Envelope<T>(bool Success, T? Data);
}
public sealed class TestAuthentication(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var role = Request.Headers["X-Test-Role"].ToString();
        if (role is not "Admin" and not "User") return Task.FromResult(AuthenticateResult.NoResult());
        var identity = new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, "11111111-1111-1111-1111-111111111111"), new Claim(ClaimTypes.Role, role)], Scheme.Name);
        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme.Name)));
    }
}
