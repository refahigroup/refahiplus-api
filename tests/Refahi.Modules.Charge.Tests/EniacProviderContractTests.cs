using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Refahi.Modules.Charge.Application.Contracts.Providers;
using Refahi.Modules.Charge.Domain.Enums;
using Refahi.Modules.Charge.Domain.Aggregates;
using Refahi.Modules.Charge.Domain.Repositories;
using Refahi.Modules.Charge.Infrastructure.Providers.Eniac;
using System.Net;
using System.Text;

namespace Refahi.Modules.Charge.Tests;

public sealed class EniacProviderContractTests
{
    [Fact]
    public void Typed_http_client_resolves_with_audited_constructor()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.Configure<EniacOptions>(options =>
        {
            options.BaseUrl = "https://provider.test";
            options.Username = "u";
            options.Password = "p";
        });
        services.AddScoped<IProviderCallLogRepository, RecordingProviderCallLogRepository>();
        services.AddHttpClient<EniacApiClient>();

        using var provider = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true
        });
        using var scope = provider.CreateScope();

        Assert.NotNull(scope.ServiceProvider.GetRequiredService<EniacApiClient>());
    }

    [Fact]
    public async Task Product_contract_maps_documented_fields()
    {
        var handler = new QueueHandler(
            Json("""{"data":{"token":"token","expiration":"2099-01-01T00:00:00+03:30"},"success":true}"""),
            Json("""{"data":[{"operatorPackageId":"O23285","titleFa":"بسته روزانه","packageTypeCode":1,"packageTypeName":"روزانه","operatorTypesId":1,"amount":10000,"amountWithTax":11000,"durationDays":1,"isActive":true,"packageCategoryId":1002}],"success":true,"eniacResultCode":0}"""));
        var provider = Provider(handler);
        var products = await provider.GetProductsAsync(ChargeOperator.Irancell, default);
        var product = Assert.Single(products);
        Assert.Equal("O23285", product.ProviderProductId);
        Assert.Equal(11_000, product.AmountWithTaxMinor);
        Assert.Equal(1002, product.ProductCategory);
    }

    [Fact]
    public async Task Purchase_transport_failure_is_not_retried()
    {
        var handler = new ThrowingPurchaseHandler();
        var logs = new RecordingProviderCallLogRepository();
        var provider = Provider(handler, logs);
        var exception = await Assert.ThrowsAsync<ChargeProviderException>(() => provider.PurchaseAsync(new ProviderPurchaseRequest(
            ChargeOperator.Irancell, ChargeServiceType.DirectCharge, null, "09350000000", 50_000,
            "CHG1", "CUSTOM", 1001, 0, 102, null, null, 1), default));
        Assert.Equal(1, handler.PurchaseCalls);
        Assert.Equal(ChargeProviderFailureKind.Transport, exception.FailureKind);
        Assert.Single(logs.Items);
        Assert.Equal(ProviderCallOutcome.TransportError, logs.Items[0].Outcome);
    }

    [Fact]
    public async Task Purchase_http_400_with_provider_code_is_definitive()
    {
        var handler = new QueueHandler(
            Json("""{"data":{"token":"token","expiration":"2099-01-01T00:00:00+03:30"},"success":true}"""),
            Json("""{"message":"شما امکان دسترسی به این سرویس را ندارید.","success":false,"eniacResultCode":120}""",
                HttpStatusCode.BadRequest));
        var logs = new RecordingProviderCallLogRepository();
        var provider = Provider(handler, logs);

        var exception = await Assert.ThrowsAsync<ChargeProviderException>(() => provider.PurchaseAsync(
            new ProviderPurchaseRequest(
                ChargeOperator.Irancell, ChargeServiceType.PinCharge, null, "09350000000", 50_000,
                "CHG1", "IrancellCharge_50000", 1003, 0, 102, null, 1005, 1), default));

        Assert.Equal(ChargeProviderFailureKind.ProviderRejected, exception.FailureKind);
        Assert.False(exception.OutcomeAmbiguous);
        Assert.False(exception.Retryable);
        Assert.Equal(120, exception.ProviderResultCode);
        var log = Assert.Single(logs.Items);
        Assert.Equal(120, log.ProviderResultCode);
        Assert.Equal(ProviderCallOutcome.ProviderRejected, log.Outcome);
    }

    [Fact]
    public async Task Trace_http_failure_does_not_turn_unknown_purchase_into_definitive_failure()
    {
        var handler = new QueueHandler(
            Json("""{"data":{"token":"token","expiration":"2099-01-01T00:00:00+03:30"},"success":true}"""),
            Json("""{"message":"دسترسی به گزارش تراکنش ممکن نیست","success":false,"eniacResultCode":120}""",
                HttpStatusCode.Forbidden));
        var provider = Provider(handler);

        var exception = await Assert.ThrowsAsync<ChargeProviderException>(() => provider.TraceAsync(
            new ProviderTraceRequest(1, "CHG1", "IrancellCharge_50000", 50_000,
                new DateOnly(2026, 7, 20)), default));

        Assert.Equal("Trace", exception.Operation);
        Assert.True(exception.OutcomeAmbiguous);
        Assert.Equal(120, exception.ProviderResultCode);
    }

    [Fact]
    public async Task Purchase_server_failure_remains_ambiguous_even_with_provider_code()
    {
        var handler = new QueueHandler(
            Json("""{"data":{"token":"token","expiration":"2099-01-01T00:00:00+03:30"},"success":true}"""),
            Json("""{"message":"خطای داخلی تامین‌کننده","success":false,"eniacResultCode":503}""",
                HttpStatusCode.InternalServerError));
        var provider = Provider(handler);

        var exception = await Assert.ThrowsAsync<ChargeProviderException>(() => provider.PurchaseAsync(
            new ProviderPurchaseRequest(
                ChargeOperator.Irancell, ChargeServiceType.DirectCharge, null, "09350000000", 50_000,
                "CHG1", "CUSTOM", 1001, 0, 102, null, null, 1), default));

        Assert.True(exception.OutcomeAmbiguous);
        Assert.Equal(503, exception.ProviderResultCode);
    }

    private static EniacChargeProvider Provider(HttpMessageHandler handler, IProviderCallLogRepository? logs = null)
    {
        var http = new HttpClient(handler) { BaseAddress = new Uri("https://provider.test") };
        var options = Options.Create(new EniacOptions { Username = "u", Password = "p" });
        return new EniacChargeProvider(logs is null
            ? new EniacApiClient(http, options, NullLogger<EniacApiClient>.Instance)
            : new EniacApiClient(http, options, logs, NullLogger<EniacApiClient>.Instance));
    }
    private static HttpResponseMessage Json(string json, HttpStatusCode statusCode = HttpStatusCode.OK) =>
        new(statusCode) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

    private sealed class QueueHandler(params HttpResponseMessage[] responses) : HttpMessageHandler
    {
        private readonly Queue<HttpResponseMessage> _responses = new(responses);
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) => Task.FromResult(_responses.Dequeue());
    }
    private sealed class ThrowingPurchaseHandler : HttpMessageHandler
    {
        public int PurchaseCalls { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            if (request.RequestUri!.AbsolutePath.EndsWith("/GetToken"))
                return Task.FromResult(Json("""{"data":{"token":"token","expiration":"2099-01-01T00:00:00+03:30"},"success":true}"""));
            PurchaseCalls++; throw new HttpRequestException("timeout");
        }
    }

    private sealed class RecordingProviderCallLogRepository : IProviderCallLogRepository
    {
        public List<ProviderCallLog> Items { get; } = [];
        public Task AddAsync(ProviderCallLog log, CancellationToken ct = default) { Items.Add(log); return Task.CompletedTask; }
        public Task<IReadOnlyList<ProviderCallLog>> GetForChargeRequestAsync(Guid requestId, int skip, int take, CancellationToken ct = default) => Task.FromResult<IReadOnlyList<ProviderCallLog>>([]);
        public Task<int> DeleteOlderThanAsync(DateTime cutoffUtc, int take, CancellationToken ct = default) => Task.FromResult(0);
        public Task SaveChangesAsync(CancellationToken ct = default) => Task.CompletedTask;
    }
}
