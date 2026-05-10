using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Polly;
using Polly.Extensions.Http;
using Refahi.Modules.PaymentGateway.Infrastructure.Providers.Jibit.Api;
using Refahi.Modules.PaymentGateway.Infrastructure.Providers.Jibit.Config;
using Refahi.Shared.Extensions;
using System;
using System.Net.Http;

namespace Refahi.Modules.PaymentGateway.Infrastructure.Providers.Jibit;

internal static class DI
{
    public static IServiceCollection UseJibitProvider(this IServiceCollection services, IConfiguration config)
    {
        services.Configure<JibitOptions>(config.GetSection("PaymentGateway:Providers:Jibit"));

        // Replace environment variables (e.g. ${REFAHIPLUS_JIBIT_API_KEY})
        services.PostConfigure<JibitOptions>(options =>
        {
            options.ApiKey = options.ApiKey.ReplaceWithEnvironmentVariables();
            options.SecretKey = options.SecretKey.ReplaceWithEnvironmentVariables();
        });

        services.AddHttpClient<JibitApiClient>((sp, client) =>
        {
            var opts = sp.GetRequiredService<IOptions<JibitOptions>>().Value;
            client.BaseAddress = new Uri(opts.BaseUrl.TrimEnd('/') + "/");
            client.Timeout = TimeSpan.FromSeconds(opts.TimeoutSeconds);
        })
        .AddPolicyHandler((sp, _) => CreateResiliencePolicy(sp));

        services.AddScoped<JibitPaymentGatewayProvider>();

        return services;
    }

    private static IAsyncPolicy<HttpResponseMessage> CreateResiliencePolicy(IServiceProvider sp)
    {
        var opts = sp.GetRequiredService<IOptionsMonitor<JibitOptions>>().CurrentValue;

        var retry = HttpPolicyExtensions
            .HandleTransientHttpError()
            .WaitAndRetryAsync(
                retryCount: opts.RetryCount,
                sleepDurationProvider: _ => TimeSpan.FromMilliseconds(opts.RetryDelayMilliseconds));

        var circuitBreaker = HttpPolicyExtensions
            .HandleTransientHttpError()
            .CircuitBreakerAsync(
                handledEventsAllowedBeforeBreaking: opts.CircuitBreakerFailuresBeforeTrip,
                durationOfBreak: TimeSpan.FromSeconds(opts.CircuitBreakerDurationSeconds));

        var timeout = Policy.TimeoutAsync<HttpResponseMessage>(
            TimeSpan.FromSeconds(opts.TimeoutSeconds));

        return Policy.WrapAsync(retry, circuitBreaker, timeout);
    }
}
