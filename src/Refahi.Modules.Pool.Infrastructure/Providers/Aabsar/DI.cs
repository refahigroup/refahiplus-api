using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Refahi.Modules.Pool.Infrastructure.Providers.Aabsar.Abstraction;
using Refahi.Modules.Pool.Infrastructure.Providers.Aabsar.Options;
using System.Net.Http.Headers;

namespace Refahi.Modules.Pool.Infrastructure.Providers.Aabsar;

public static class DI
{
    public static IServiceCollection AddAabsarProvider(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        
        services
            .AddOptions<AabsarOptions>()
            .Bind(configuration.GetSection(AabsarOptions.SectionName))
            .Validate(
                options => !string.IsNullOrWhiteSpace(options.AccessToken),
                $"{AabsarOptions.SectionName}:AccessToken is required.")
            .Validate(
                options => TryCreateBaseUri(options.BaseUrl, out _),
                $"{AabsarOptions.SectionName}:BaseUrl must be a valid absolute HTTP/HTTPS URL.")
            .ValidateOnStart();

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

            httpClient.DefaultRequestHeaders.Accept.Clear();
            httpClient.DefaultRequestHeaders.Accept.Add(
                new MediaTypeWithQualityHeaderValue("application/json"));

            // Use the exact documented hyphenated header name.
            // Do not replace it with access_token in production.
            httpClient.DefaultRequestHeaders.Remove("access-token");
            httpClient.DefaultRequestHeaders.TryAddWithoutValidation("access-token", options.AccessToken);
        });

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

