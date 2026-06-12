using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Polly;
using Polly.Extensions.Http;
using Refahi.Modules.Flights.Infrastructure.Providers.SnappTrip.Api;
using Refahi.Modules.Flights.Infrastructure.Providers.SnappTrip.Config;
using Refahi.Shared.Extensions;

namespace Refahi.Modules.Flights.Infrastructure.Providers.SnappTrip;

internal static class DI
{
    public static IServiceCollection UseSnappTripFlightProvider(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<SnappTripFlightOptions>(
            configuration.GetSection("Flights:Providers:SnappTrip"));

        services.PostConfigure<SnappTripFlightOptions>(options =>
        {
            options.ApiKey = options.ApiKey.ReplaceWithEnvironmentVariables().Trim();
        });

        services.AddHttpClient<SnappTripFlightApiClient>((sp, client) =>
            {
                var options = sp.GetRequiredService<IOptions<SnappTripFlightOptions>>().Value;

                client.BaseAddress = new Uri(options.BaseUrl);
                client.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);

                if (string.IsNullOrWhiteSpace(options.ApiKey) ||
                    options.ApiKey.Contains('{', StringComparison.Ordinal) ||
                    options.ApiKey.Contains('}', StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        "SnappTrip Flight API key is not configured or contains an unresolved environment variable placeholder.");
                }

                if (!string.IsNullOrWhiteSpace(options.ApiKey))
                    client.DefaultRequestHeaders.Add("api-key", options.ApiKey);
            });
            //.AddPolicyHandler((sp, _) => CreateResiliencePolicy(sp));

        services.AddScoped<SnappTripFlightProvider>();

        return services;
    }

    private static IAsyncPolicy<HttpResponseMessage> CreateResiliencePolicy(IServiceProvider sp)
    {
        var options = sp.GetRequiredService<IOptionsMonitor<SnappTripFlightOptions>>().CurrentValue;

        var bulkhead = Policy.BulkheadAsync<HttpResponseMessage>(
            options.BulkheadMaxParallelization,
            options.BulkheadMaxQueuedActions);

        var retry = HttpPolicyExtensions
            .HandleTransientHttpError()
            .WaitAndRetryAsync(
                options.RetryCount,
                _ => TimeSpan.FromMilliseconds(options.RetryDelayMilliseconds));

        var circuitBreaker = HttpPolicyExtensions
            .HandleTransientHttpError()
            .CircuitBreakerAsync(
                options.CircuitBreakerFailuresBeforeTrip,
                TimeSpan.FromSeconds(options.CircuitBreakerDurationSeconds));

        var timeout = Policy.TimeoutAsync<HttpResponseMessage>(
            TimeSpan.FromSeconds(options.TimeoutSeconds));

        return Policy.WrapAsync(bulkhead, retry, circuitBreaker, timeout);
    }
}
