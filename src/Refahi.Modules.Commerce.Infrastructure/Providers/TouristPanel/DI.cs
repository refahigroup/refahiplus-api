using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Refahi.Modules.Commerce.Application.Contracts.Providers;
namespace Refahi.Modules.Commerce.Infrastructure.Providers.TouristPanel;

public static class DI
{
    public static IServiceCollection AddTouristPanelProvider(this IServiceCollection services, IConfiguration configuration)
    {
        var settings = configuration
            .GetSection(TouristPanelOptions.SectionName)
            .Get<TouristPanelOptions>() ?? new();

        settings.Validate();

        services.Configure<TouristPanelOptions>(
            configuration.GetSection(TouristPanelOptions.SectionName)
        );

        if (!settings.Enabled) 
            return services;

        services.AddMemoryCache();

        services.AddHttpClient("TouristPanel.Token", http => 
            http.Timeout = TimeSpan.FromSeconds(settings.TimeoutSeconds)
        ).ConfigurePrimaryHttpMessageHandler(() => 
            new SocketsHttpHandler 
            { 
                ConnectTimeout = TimeSpan.FromSeconds(15), 
                AllowAutoRedirect = false 
            }
        );

        services.AddSingleton(sp => 
            new TouristPanelTokenProvider(
                sp.GetRequiredService<IHttpClientFactory>().CreateClient("TouristPanel.Token"),
                sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<TouristPanelOptions>>()
            )
        );

        services.AddHttpClient<TouristPanelClient>(

            http => http.Timeout = TimeSpan.FromSeconds(settings.TimeoutSeconds)

        ).ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler 
        { 
            ConnectTimeout = TimeSpan.FromSeconds(15), 
            AllowAutoRedirect = false 
        });

        services.AddScoped<TouristPanelCatalog>()
                .AddScoped<TouristPanelReadCache>()
                .AddScoped<ICommerceProvider, TouristPanelCommerceProvider>();

        services.AddHostedService<TouristPanelCatalogWorker>();

        return services;
    }
}
