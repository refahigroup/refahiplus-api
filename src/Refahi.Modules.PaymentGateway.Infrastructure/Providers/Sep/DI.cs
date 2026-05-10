using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Polly;
using Polly.Extensions.Http;
using Refahi.Modules.PaymentGateway.Infrastructure.Providers.Sep.Api;
using Refahi.Modules.PaymentGateway.Infrastructure.Providers.Sep.Config;
using Refahi.Shared.Extensions;
using System;
using System.Net.Http;

namespace Refahi.Modules.PaymentGateway.Infrastructure.Providers.Sep;

internal static class DI
{
    public static IServiceCollection UseSepProvider(this IServiceCollection services, IConfiguration config)
    {
        services.Configure<SepOptions>(config.GetSection("PaymentGateway:Providers:Sep"));

        // Replace environment variables (e.g. ${REFAHIPLUS_SEP_TERMINAL_ID})
        services.PostConfigure<SepOptions>(options =>
        {
            options.TerminalId = options.TerminalId.ReplaceWithEnvironmentVariables();
        });

        services.AddHttpClient<SepApiClient>((sp, client) =>
        {
            var opts = sp.GetRequiredService<IOptions<SepOptions>>();
            client.Timeout = TimeSpan.FromSeconds(opts.Value.TimeoutSeconds);
        })
        .AddPolicyHandler((sp, _) => CreateResiliencePolicy(sp));

        services.AddScoped<SepPaymentGatewayProvider>();

        return services;
    }

    private static IAsyncPolicy<HttpResponseMessage> CreateResiliencePolicy(IServiceProvider sp)
    {
        var opts = sp.GetRequiredService<IOptionsMonitor<SepOptions>>().CurrentValue;

        // Retry برای خطاهای موقت HTTP
        var retry = HttpPolicyExtensions
            .HandleTransientHttpError()
            .WaitAndRetryAsync(
                retryCount: opts.RetryCount,
                sleepDurationProvider: _ => TimeSpan.FromMilliseconds(opts.RetryDelayMilliseconds));

        // Circuit Breaker
        var circuitBreaker = HttpPolicyExtensions
            .HandleTransientHttpError()
            .CircuitBreakerAsync(
                handledEventsAllowedBeforeBreaking: opts.CircuitBreakerFailuresBeforeTrip,
                durationOfBreak: TimeSpan.FromSeconds(opts.CircuitBreakerDurationSeconds));

        // Timeout سطح Polly
        var timeout = Policy.TimeoutAsync<HttpResponseMessage>(
            TimeSpan.FromSeconds(opts.TimeoutSeconds));

        return Policy.WrapAsync(retry, circuitBreaker, timeout);
    }
}
