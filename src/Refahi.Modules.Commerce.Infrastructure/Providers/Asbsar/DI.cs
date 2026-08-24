using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Refahi.Modules.Commerce.Application.Contracts.Providers;
using Refahi.Modules.Commerce.Infrastructure.Providers.Asbsar.Abstraction;
using Refahi.Modules.Commerce.Infrastructure.Providers.Asbsar.Options;
using System.Net.Http.Headers;
using Polly;
using Polly.Extensions.Http;

namespace Refahi.Modules.Commerce.Infrastructure.Providers.Asbsar;

public static class DI
{
    public static IServiceCollection AddAabsarProvider(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        
        var enabled = configuration.GetValue<bool>($"{AabsarOptions.SectionName}:Enabled");
        services
            .AddOptions<AabsarOptions>()
            .Bind(configuration.GetSection(AabsarOptions.SectionName))
            .Validate(
                options => !options.Enabled || !string.IsNullOrWhiteSpace(options.AccessToken),
                $"{AabsarOptions.SectionName}:AccessToken is required.")
            .Validate(
                options => TryCreateBaseUri(options.BaseUrl, out _),
                $"{AabsarOptions.SectionName}:BaseUrl must be a valid absolute HTTP/HTTPS URL.")
            .ValidateOnStart();

        services.AddTransient<AabsarObservabilityHandler>();
        services.AddHttpClient<IAabsarApiClient, AabsarApiClient>((serviceProvider, httpClient) =>
        {
            var options = serviceProvider
                .GetRequiredService<IOptionsMonitor<AabsarOptions>>()
                .CurrentValue;

            if (!TryCreateBaseUri(options.BaseUrl, out var baseUri))
            {
                throw new OptionsValidationException(
                    AabsarOptions.SectionName,
                    typeof(AabsarOptions),
                    new[] { "BaseUrl must be a valid absolute HTTP/HTTPS URL." });
            }

            httpClient.BaseAddress = baseUri;
            httpClient.Timeout = TimeSpan.FromSeconds(Math.Clamp(options.TimeoutSeconds, 3, 60));

            httpClient.DefaultRequestHeaders.Accept.Clear();
            httpClient.DefaultRequestHeaders.Accept.Add(
                new MediaTypeWithQualityHeaderValue("application/json"));

            // Use the exact documented hyphenated header name.
            // Do not replace it with access_token in production.
            httpClient.DefaultRequestHeaders.Remove("access-token");
            httpClient.DefaultRequestHeaders.TryAddWithoutValidation("access-token", options.AccessToken);
        })
        .AddHttpMessageHandler<AabsarObservabilityHandler>()
        .AddPolicyHandler(request => request.Method == HttpMethod.Get
            ? HttpPolicyExtensions.HandleTransientHttpError().WaitAndRetryAsync(2, retry => TimeSpan.FromMilliseconds(150 * retry))
            : Policy.NoOpAsync<HttpResponseMessage>());

        if (enabled)
        {
            services.AddScoped<ICommerceProvider, AabsarCommerceProvider>();
            services.AddHealthChecks().AddCheck<AabsarHealthCheck>("commerce-aabsar", tags: ["commerce", "provider"]);
        }

        return services;
    }

    private static bool TryCreateBaseUri(string? configuredUrl, out Uri? baseUri)
    {
        var value = string.IsNullOrWhiteSpace(configuredUrl)
            ? AabsarOptions.DefaultBaseUrl
            : configuredUrl.Trim();

        if (!value.EndsWith("/", StringComparison.Ordinal))
        {
            value += "/";
        }

        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp))
        {
            baseUri = null;
            return false;
        }

        baseUri = uri;
        return true;
    }
}

