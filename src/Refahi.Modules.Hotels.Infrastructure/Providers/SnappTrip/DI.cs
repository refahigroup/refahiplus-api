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
        // Register SnappTripOptions configuration
        services.Configure<SnappTripOptions>(config.GetSection("SnappTrip"));

        // Register SnappTripApiClient with HttpClient
        services.AddHttpClient<SnappTripApiClient>((sp, client) =>
        {
            var opts = sp.GetRequiredService<IOptions<SnappTripOptions>>().Value;

            client.BaseAddress = new Uri(opts.BaseUrl);
            client.DefaultRequestHeaders.Add("api-key", opts.ApiKey);
            client.Timeout = TimeSpan.FromSeconds(opts.TimeoutSeconds);
        })
        .AddPolicyHandler((sp, _) => CreateResiliencePolicy(sp));

        services.AddScoped<SnappTripHotelProvider>();

        return services;
    }

    private static IAsyncPolicy<HttpResponseMessage> CreateResiliencePolicy(IServiceProvider sp)
    {
        var opts = sp.GetRequiredService<IOptions<SnappTripOptions>>().Value;

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
