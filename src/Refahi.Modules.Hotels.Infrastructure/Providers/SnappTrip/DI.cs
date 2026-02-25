using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Polly;
using Polly.Extensions.Http;
using Refahi.Modules.Hotels.Infrastructure.Providers.SnappTrip.Api;
using Refahi.Modules.Hotels.Infrastructure.Providers.SnappTrip.Config;

namespace Refahi.Modules.Hotels.Infrastructure.Providers.SnappTrip;

internal static class DI
{
    public static IServiceCollection UseSnappTripProvider(this IServiceCollection services, IConfiguration config)
    {
        services.Configure<SnappTripOptions>(config.GetSection("Hotels:Providers:SnappTrip"));

        services.AddHttpClient<SnappTripApiClient>((sp, client) =>
        {
            var opts = sp.GetRequiredService<IOptions<SnappTripOptions>>();

            client.BaseAddress = new Uri(opts.Value.BaseUrl);
            client.DefaultRequestHeaders.Add("api-key", opts.Value.ApiKey);
            client.Timeout = TimeSpan.FromSeconds(opts.Value.TimeoutSeconds);
        })
        .AddPolicyHandler((sp, _) => CreateResiliencePolicy(sp));

        services.AddScoped<SnappTripHotelProvider>();

        return services;
    }

    private static IAsyncPolicy<HttpResponseMessage> CreateResiliencePolicy(IServiceProvider sp)
    {
        var opts = sp.GetRequiredService<IOptionsMonitor<SnappTripOptions>>().CurrentValue;

        // Bulkhead: محدود کردن تعداد درخواست‌های همزمان
        var bulkhead = Policy.BulkheadAsync<HttpResponseMessage>(
            maxParallelization: opts.BulkheadMaxParallelization,
            maxQueuingActions: opts.BulkheadMaxQueuedActions);

        // Retry برای خطاهای موقت HTTP
        var retry = HttpPolicyExtensions
            .HandleTransientHttpError()
            .WaitAndRetryAsync(
                retryCount: opts.RetryCount,
                sleepDurationProvider: attempt =>
                    TimeSpan.FromMilliseconds(opts.RetryDelayMilliseconds));

        // CircuitBreaker: اگر پشت سر هم چند خطای موقت رخ داد، قطع موقت
        var circuitBreaker = HttpPolicyExtensions
            .HandleTransientHttpError()
            .CircuitBreakerAsync(
                handledEventsAllowedBeforeBreaking: opts.CircuitBreakerFailuresBeforeTrip,
                durationOfBreak: TimeSpan.FromSeconds(opts.CircuitBreakerDurationSeconds));

        // Timeout سطح Polly (اضافه بر HttpClient.Timeout)
        var timeout = Policy.TimeoutAsync<HttpResponseMessage>(
            TimeSpan.FromSeconds(opts.TimeoutSeconds));

        // ترتیب مهم است: اول Bulkhead، بعد Retry، بعد CircuitBreaker، بعد Timeout
        return Policy.WrapAsync(bulkhead, retry, circuitBreaker, timeout);
    }
}
