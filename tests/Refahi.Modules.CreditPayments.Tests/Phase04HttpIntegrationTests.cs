using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Refahi.Modules.CreditPayments.Api;
using Refahi.Modules.CreditPayments.Domain;
using Refahi.Modules.CreditPayments.Infrastructure.Persistence;
using Testcontainers.PostgreSql;

namespace Refahi.Modules.CreditPayments.Tests;

public sealed class Phase04HttpIntegrationTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer postgres = new PostgreSqlBuilder()
        .WithImage("postgres:17-alpine")
        .Build();
    private WebApplicationFactory<Program> factory = null!;
    private HttpClient client = null!;
    private Credential active = null!;
    private Credential readOnly = null!;
    private Credential suspendedMerchant = null!;
    private Credential suspendedTerminal = null!;

    public async Task InitializeAsync()
    {
        await postgres.StartAsync();
        factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Commerce:Providers:TouristPanel:Enabled", "false");
            builder.UseSetting("Commerce:Providers:TouristPanel:SalesEnabled", "false");
            builder.UseSetting("Commerce:Providers:Aabsar:Enabled", "false");
            builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["ConnectionStringName"] = "Default",
                    ["ConnectionStrings:Default"] = postgres.GetConnectionString(),
                    ["CreditPayments:Checkout:PublicBaseUrl"] = "https://checkout.test",
                    ["CreditPayments:Checkout:TokenKeys:1"] =
                        "cGhhc2UtMDQtaHR0cC1pbnRlZ3JhdGlvbi10ZXN0LWtleQ=="
                }));
        });
        client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false
        });

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CreditPaymentsDbContext>();
        await db.Database.MigrateAsync();
        var secrets = scope.ServiceProvider.GetRequiredService<ICredentialSecretService>();
        active = await SeedAsync(db, secrets, "active", [CredentialScopes.PaymentCreate, CredentialScopes.PaymentRead]);
        readOnly = await SeedAsync(db, secrets, "read-only", [CredentialScopes.PaymentRead]);
        suspendedMerchant = await SeedAsync(db, secrets, "suspended-merchant", [CredentialScopes.PaymentCreate], suspendMerchant: true);
        suspendedTerminal = await SeedAsync(db, secrets, "suspended-terminal", [CredentialScopes.PaymentCreate], suspendTerminal: true);
    }

    public async Task DisposeAsync()
    {
        client.Dispose();
        await factory.DisposeAsync();
        await postgres.DisposeAsync();
    }

    [Fact]
    public async Task Public_create_is_atomic_and_idempotent_through_the_real_HTTP_host()
    {
        var body = Payment("HTTP-ORDER-1");
        using var first = await SendCreateAsync(active, "http-idempotency-1", body);
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        var firstData = await DataAsync(first);
        Assert.Equal("awaiting_customer", firstData.GetProperty("status").GetString());
        var paymentId = firstData.GetProperty("paymentId").GetGuid();
        var checkoutUrl = firstData.GetProperty("checkoutUrl").GetString();
        var expiresAt = firstData.GetProperty("expiresAt").GetDateTimeOffset();
        Assert.StartsWith("https://checkout.test/c/", checkoutUrl, StringComparison.Ordinal);

        using var repeated = await SendCreateAsync(active, "http-idempotency-1", body);
        Assert.Equal(HttpStatusCode.Created, repeated.StatusCode);
        var repeatedData = await DataAsync(repeated);
        Assert.Equal(paymentId, repeatedData.GetProperty("paymentId").GetGuid());
        Assert.Equal(checkoutUrl, repeatedData.GetProperty("checkoutUrl").GetString());
        Assert.True((expiresAt - repeatedData.GetProperty("expiresAt").GetDateTimeOffset()).Duration()
            < TimeSpan.FromMicroseconds(1));

        var concurrent = await Task.WhenAll(Enumerable.Range(0, 6)
            .Select(_ => SendCreateAsync(active, "http-idempotency-concurrent", Payment("HTTP-ORDER-2"))));
        try
        {
            Assert.All(concurrent, response => Assert.Equal(HttpStatusCode.Created, response.StatusCode));
            var results = await Task.WhenAll(concurrent.Select(DataAsync));
            Assert.Single(results.Select(x => x.GetProperty("paymentId").GetGuid()).Distinct());
            Assert.Single(results.Select(x => x.GetProperty("checkoutUrl").GetString()).Distinct());
        }
        finally
        {
            foreach (var response in concurrent) response.Dispose();
        }

        using var conflict = await SendCreateAsync(active, "http-idempotency-1", Payment("HTTP-ORDER-DIFFERENT"));
        Assert.Equal(HttpStatusCode.Conflict, conflict.StatusCode);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CreditPaymentsDbContext>();
        Assert.Equal(2, await db.GatewayPayments.CountAsync());
        Assert.Equal(2, await db.CheckoutSessions.CountAsync());
        Assert.Equal(2, await db.IdempotencyRecords.CountAsync(x => x.State == IdempotencyState.Completed));
        var rawToken = checkoutUrl!.Split('/').Last();
        var persistedCheckoutMaterial = (await db.CheckoutSessions.AsNoTracking().ToListAsync())
            .Select(x => $"{x.Id:D}:{Convert.ToHexString(x.TokenHash)}:{Convert.ToHexString(x.TokenNonce)}");
        Assert.DoesNotContain(rawToken, string.Join('|', persistedCheckoutMaterial), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Basic_auth_and_scope_failures_are_normalized()
    {
        using var noAuth = await client.PostAsJsonAsync(Path("/payments"), Payment("AUTH-1"));
        Assert.Equal(HttpStatusCode.Unauthorized, noAuth.StatusCode);

        using var malformedRequest = new HttpRequestMessage(HttpMethod.Post, Path("/payments"))
        {
            Content = JsonContent.Create(Payment("AUTH-2"))
        };
        malformedRequest.Headers.Authorization = new AuthenticationHeaderValue("Basic", "not-base64");
        malformedRequest.Headers.Add("Idempotency-Key", "auth-malformed");
        using var malformed = await client.SendAsync(malformedRequest);
        Assert.Equal(HttpStatusCode.Unauthorized, malformed.StatusCode);

        using var invalid = await SendCreateAsync(new("missing", "invalid"), "auth-invalid", Payment("AUTH-3"));
        Assert.Equal(HttpStatusCode.Unauthorized, invalid.StatusCode);
        using var forbidden = await SendCreateAsync(readOnly, "auth-forbidden", Payment("AUTH-4"));
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
        using var merchantRejected = await SendCreateAsync(suspendedMerchant, "auth-merchant", Payment("AUTH-5"));
        Assert.Equal(HttpStatusCode.Forbidden, merchantRejected.StatusCode);
        using var terminalRejected = await SendCreateAsync(suspendedTerminal, "auth-terminal", Payment("AUTH-6"));
        Assert.Equal(HttpStatusCode.Forbidden, terminalRejected.StatusCode);
    }

    [Fact]
    public async Task Inquiry_is_scoped_checkout_is_safe_and_rate_limit_policies_are_attached()
    {
        using var created = await SendCreateAsync(active, "inquiry-1", Payment("INQUIRY-1"));
        var data = await DataAsync(created);
        var paymentId = data.GetProperty("paymentId").GetGuid();
        var checkoutUrl = new Uri(data.GetProperty("checkoutUrl").GetString()!);

        using var inquiryRequest = Authorized(HttpMethod.Get, Path($"/payments/{paymentId:D}"), active);
        using var inquiry = await client.SendAsync(inquiryRequest);
        Assert.Equal(HttpStatusCode.OK, inquiry.StatusCode);

        var token = checkoutUrl.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries).Last();
        using var checkout = await client.GetAsync(Path($"/checkout/{token}"));
        Assert.Equal(HttpStatusCode.OK, checkout.StatusCode);
        var checkoutJson = await checkout.Content.ReadAsStringAsync();
        Assert.DoesNotContain(paymentId.ToString("D"), checkoutJson, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("tokenHash", checkoutJson, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("customerId", checkoutJson, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("authentication_required", checkoutJson, StringComparison.Ordinal);

        var endpoints = factory.Services.GetServices<EndpointDataSource>()
            .SelectMany(source => source.Endpoints)
            .OfType<RouteEndpoint>()
            .Where(endpoint => endpoint.RoutePattern.RawText!.StartsWith(DI.ApiPrefix, StringComparison.Ordinal))
            .GroupBy(endpoint => endpoint.RoutePattern.RawText!)
            .ToDictionary(group => group.Key, group => group.First());
        AssertPolicy(endpoints, "/api/credit-payments/v1/payments", DI.MerchantRateLimitPolicy);
        AssertPolicy(endpoints, "/api/credit-payments/v1/payments/{paymentId:guid}/cancel", DI.MerchantRateLimitPolicy);
        AssertPolicy(endpoints, "/api/credit-payments/v1/payments/{paymentId:guid}/refunds", DI.MerchantRateLimitPolicy);
        AssertPolicy(endpoints, "/api/credit-payments/v1/checkout/{token}/otp/send", DI.OtpSendRateLimitPolicy);
        AssertPolicy(endpoints, "/api/credit-payments/v1/checkout/{token}/otp/verify", DI.OtpVerifyRateLimitPolicy);

        using var anonymousAdmin=await client.GetAsync(Path("/admin/payments"));
        Assert.Equal(HttpStatusCode.Unauthorized,anonymousAdmin.StatusCode);
        using var merchantAdminRequest=Authorized(HttpMethod.Get,Path("/admin/payments"),active);
        using var merchantAdmin=await client.SendAsync(merchantAdminRequest);
        Assert.Contains(merchantAdmin.StatusCode,new[]{HttpStatusCode.Unauthorized,HttpStatusCode.Forbidden});
    }

    [Fact]
    public async Task OpenApi_generation_includes_credit_payments_and_bearer_security()
    {
        using var response = await client.GetAsync("/api/swagger/v1/swagger.json");
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode, $"OpenAPI generation failed ({(int)response.StatusCode}): {body}");
        using var document = JsonDocument.Parse(body);
        var root = document.RootElement;
        Assert.True(root.GetProperty("paths").TryGetProperty("/api/credit-payments/v1/payments", out _));
        Assert.True(root.GetProperty("components").GetProperty("securitySchemes").TryGetProperty("Bearer", out _));
    }

    private async Task<HttpResponseMessage> SendCreateAsync(Credential credential, string key, object body)
    {
        using var request = Authorized(HttpMethod.Post, Path("/payments"), credential);
        request.Headers.Add("Idempotency-Key", key);
        request.Content = JsonContent.Create(body);
        return await client.SendAsync(request);
    }

    private static HttpRequestMessage Authorized(HttpMethod method, string path, Credential credential)
    {
        var request = new HttpRequestMessage(method, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic",
            Convert.ToBase64String(Encoding.UTF8.GetBytes($"{credential.ClientId}:{credential.Secret}")));
        return request;
    }

    private static object Payment(string orderId) => new
    {
        merchantOrderId = orderId,
        amountMinor = 500,
        currency = "IRR",
        description = "phase 04 HTTP test",
        returnUrl = "https://merchant.example/callback",
        expiresInSeconds = 900,
        metadata = new Dictionary<string, string> { ["source"] = "test" }
    };

    private static async Task<JsonElement> DataAsync(HttpResponseMessage response)
    {
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.GetProperty("data").Clone();
    }

    private static string Path(string suffix) => DI.ApiPrefix + suffix;

    private static void AssertPolicy(
        IReadOnlyDictionary<string, RouteEndpoint> endpoints,
        string route,
        string expectedPolicy)
    {
        var metadata = endpoints[route].Metadata.GetMetadata<EnableRateLimitingAttribute>();
        Assert.NotNull(metadata);
        Assert.Equal(expectedPolicy, metadata.PolicyName);
    }

    private static async Task<Credential> SeedAsync(
        CreditPaymentsDbContext db,
        ICredentialSecretService secrets,
        string name,
        string[] scopes,
        bool suspendMerchant = false,
        bool suspendTerminal = false)
    {
        var now = DateTimeOffset.UtcNow;
        var merchant = Merchant.Create(Guid.NewGuid(), $"Merchant {name}", now);
        merchant.Activate(now);
        var terminal = merchant.AddTerminal($"Terminal {name}", "store", 100, 10_000,
            ["https://merchant.example/callback"], now);
        var material = secrets.Generate();
        terminal.AddCredential(material.ClientId, material.Hash, material.Algorithm, material.Version,
            scopes, name, null, now);
        if (suspendTerminal) terminal.Suspend(now);
        if (suspendMerchant) merchant.Suspend(now);
        db.Merchants.Add(merchant);
        await db.SaveChangesAsync();
        return new(material.ClientId, material.Secret);
    }

    private sealed record Credential(string ClientId, string Secret);
}
