using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Polly;
using Polly.Extensions.Http;
using Refahi.Modules.Hotels.Infrastructure.Providers.SnappTrip.Api;
using Refahi.Modules.Hotels.Infrastructure.Providers.SnappTrip.Config;
using Refahi.Shared.Extensions;

namespace Refahi.Modules.Hotels.Infrastructure.Providers.SnappTrip;

internal static class DI
{
    public static IServiceCollection UseSnappTripProvider(
        this IServiceCollection services,
        IConfiguration config
    )
    {
        services.Configure<SnappTripOptions>(config.GetSection("Hotels:Providers:SnappTrip"));

        // Replace environment variables in ApiKey
        services.PostConfigure<SnappTripOptions>(options =>
        {
            options.ApiKey = options.ApiKey.ReplaceWithEnvironmentVariables();
        });

        var policyOptions = config.GetSection("Hotels:Providers:SnappTrip").Get<SnappTripOptions>() ?? new SnappTripOptions();
        var discoveryPolicy = CreateResiliencePolicy(policyOptions, false);
        var bookingPolicy = CreateResiliencePolicy(policyOptions, true);
        services
            .AddHttpClient<SnappTripApiClient>(
                (sp, client) =>
                {
                    var opts = sp.GetRequiredService<IOptions<SnappTripOptions>>();

                    client.BaseAddress = new Uri(opts.Value.BaseUrl);
                    client.DefaultRequestHeaders.Add("api-key", opts.Value.ApiKey);
                    client.Timeout = TimeSpan.FromSeconds(opts.Value.TimeoutSeconds);
                }
            )
            .AddPolicyHandler((_, request) => request.RequestUri?.AbsolutePath.StartsWith("/booking/", StringComparison.OrdinalIgnoreCase) == true && request.Method != HttpMethod.Get ? bookingPolicy : discoveryPolicy);

        services.AddScoped<SnappTripHotelProvider>();

        return services;
    }

    private static IAsyncPolicy<HttpResponseMessage> CreateResiliencePolicy(SnappTripOptions opts, bool mutation)
    {

        // Bulkhead: محدود کردن تعداد درخواست‌های همزمان
        var bulkhead = Policy.BulkheadAsync<HttpResponseMessage>(
            maxParallelization: opts.BulkheadMaxParallelization,
            maxQueuingActions: opts.BulkheadMaxQueuedActions
        );

        // Retry برای خطاهای موقت HTTP
        var retry = HttpPolicyExtensions
            .HandleTransientHttpError()
            .WaitAndRetryAsync(
                retryCount: opts.RetryCount,
                sleepDurationProvider: attempt =>
                    TimeSpan.FromMilliseconds(opts.RetryDelayMilliseconds)
            );

        // CircuitBreaker: اگر پشت سر هم چند خطای موقت رخ داد، قطع موقت
        var circuitBreaker = HttpPolicyExtensions
            .HandleTransientHttpError()
            .CircuitBreakerAsync(
                handledEventsAllowedBeforeBreaking: opts.CircuitBreakerFailuresBeforeTrip,
                durationOfBreak: TimeSpan.FromSeconds(opts.CircuitBreakerDurationSeconds)
            );

        // Timeout سطح Polly (اضافه بر HttpClient.Timeout)
        var timeout = Policy.TimeoutAsync<HttpResponseMessage>(
            TimeSpan.FromSeconds(opts.TimeoutSeconds)
        );

        // ترتیب مهم است: اول Bulkhead، بعد Retry، بعد CircuitBreaker، بعد Timeout
        return mutation ? Policy.WrapAsync(bulkhead, circuitBreaker, timeout) : Policy.WrapAsync(bulkhead, retry, circuitBreaker, timeout);
    }
}
