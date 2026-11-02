using System.Net;
using System.Threading.RateLimiting;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Refahi.Modules.Catalogue.Api;
using Refahi.Modules.Catalogue.Application;
using Refahi.Modules.Catalogue.Application.Contracts;
using Xunit;

namespace Refahi.Modules.Catalogue.Tests;

public sealed class CatalogueAdmissionTests
{
    private sealed class Executor : ICatalogueExecutor
    {
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public int Calls;
        public IReadOnlyList<string> RegisteredKeys { get; init; } = ["test"];
        public IReadOnlyList<string> Keys => RegisteredKeys;
        public async Task<T> ExecuteAsync<T>(string key, Func<ICatalogueSource, CancellationToken, Task<T>> action, CancellationToken ct)
        {
            Interlocked.Increment(ref Calls);
            Entered.TrySetResult();
            await Release.Task.WaitAsync(ct);
            return await action(new Source(), ct);
        }
    }
    private sealed class Source : ICatalogueSource
    {
        public Task<CatalogueSourceResult> SearchAsync(CatalogueQuery query, int take, CancellationToken ct) => Task.FromResult(new CatalogueSourceResult([], 0, false));
        public Task<IReadOnlyList<CatalogueCity>> CitiesAsync(CancellationToken ct) => Task.FromResult<IReadOnlyList<CatalogueCity>>([]);
    }

    [Fact]
    public async Task InsufficientFanoutCapacityFailsBeforeApplyingMigrations()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Logging.ClearProviders();
        builder.Services.RegisterCatalogueAdmission(new ConfigurationBuilder().Build());
        builder.Services.Configure<CatalogueOptions>(o => o.Enabled = true);
        builder.Services.AddSingleton<ICatalogueExecutor>(new Executor { RegisteredKeys = ["one", "two", "three"] });
        await using var app = builder.Build();
        var error = Assert.Throws<InvalidOperationException>(() => app.UseCatalogueModule());
        Assert.Contains("MaxConcurrency", error.Message);
    }

    [Theory]
    [InlineData("MaxConcurrentRequests", "0")]
    [InlineData("MaxConcurrency", "0")]
    [InlineData("SourceTimeoutSeconds", "5")]
    public void InvalidAdmissionSettingsFailValidation(string key, string value)
    {
        using var services = new ServiceCollection().RegisterCatalogueAdmission(new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Catalogue:"+key] = value }).Build()).BuildServiceProvider();
        Assert.Throws<Microsoft.Extensions.Options.OptionsValidationException>(() =>
            services.GetRequiredService<Microsoft.Extensions.Options.IOptions<CatalogueOptions>>().Value);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AdmissionRejectsBeforeExecutingAndRecoversAfterCompletionOrCancellation(bool cancel)
    {
        var builder = WebApplication.CreateBuilder();
        builder.Logging.ClearProviders();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Services.RegisterCatalogueAdmission(new ConfigurationBuilder().Build());
        builder.Services.Configure<CatalogueOptions>(o => { o.Enabled = true; o.MaxConcurrentRequests = 1; });
        builder.Services.AddMediatR(typeof(CatalogueService).Assembly);
        builder.Services.AddScoped<CatalogueService>();
        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen();
        var executor = new Executor();
        builder.Services.AddSingleton<ICatalogueExecutor>(executor);
        // Existing modules own this callback. Catalogue must not replace it.
        builder.Services.AddRateLimiter(o => {
            o.OnRejected = (context, ct) => { context.HttpContext.Response.StatusCode = 418; return ValueTask.CompletedTask; };
            o.AddPolicy("other", _ => RateLimitPartition.GetFixedWindowLimiter("other", _ => new FixedWindowRateLimiterOptions {
                PermitLimit = 1, Window = TimeSpan.FromMinutes(1), QueueLimit = 0
            }));
        });
        await using var app = builder.Build();
        app.UseRouting();
        app.UseRateLimiter();
        app.UseSwagger();
        new CatalogueEndpoints().Map(app);
        app.MapGet("/other", () => Results.Ok()).RequireRateLimiting("other");
        await app.StartAsync();
        var url = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
        using var http = new HttpClient { BaseAddress = new Uri(url) };
        using var swagger = await http.GetAsync("/swagger/v1/swagger.json");
        Assert.Equal(HttpStatusCode.OK, swagger.StatusCode);
        Assert.Contains("/api/catalogue/items", await swagger.Content.ReadAsStringAsync());
        using var cancellation = new CancellationTokenSource();
        var pending = http.GetAsync("/api/catalogue/items", cancellation.Token);
        await executor.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        foreach (var path in new[] { "items", "filters/cities", "filters/locations" })
        {
            using var rejected = await http.GetAsync("/api/catalogue/" + path);
            Assert.Equal(HttpStatusCode.TooManyRequests, rejected.StatusCode);
            Assert.Equal("1", rejected.Headers.RetryAfter!.ToString());
            Assert.True(rejected.Headers.CacheControl!.NoStore);
            Assert.Contains("429", await rejected.Content.ReadAsStringAsync());
        }
        Assert.Equal(1, executor.Calls);
        if (cancel)
        {
            cancellation.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);
        }
        else
        {
            executor.Release.SetResult();
            using var completed = await pending;
            Assert.Equal(HttpStatusCode.OK, completed.StatusCode);
        }
        executor.Release.TrySetResult();
        // Client cancellation completes before the server necessarily observes RequestAborted.
        using var recoveryDeadline = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        while (true)
        {
            using var recovered = await http.GetAsync("/api/catalogue/items", recoveryDeadline.Token);
            if (recovered.StatusCode == HttpStatusCode.OK) break;
            Assert.Equal(HttpStatusCode.TooManyRequests, recovered.StatusCode);
            await Task.Delay(10, recoveryDeadline.Token);
        }
        Assert.Equal(HttpStatusCode.OK, (await http.GetAsync("/other")).StatusCode);
        Assert.Equal(418, (int)(await http.GetAsync("/other")).StatusCode);
        await app.StopAsync();
    }
}
