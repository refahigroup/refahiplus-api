using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Refahi.Modules.Entertainments.Application.Contracts;
using Refahi.Modules.Entertainments.Application.Contracts.Providers;
using Refahi.Modules.Entertainments.Domain;
using Refahi.Modules.Entertainments.Infrastructure.Persistence;
using Refahi.Modules.Entertainments.Infrastructure.Providers.TouristPanel;
using Refahi.Modules.Entertainments.Infrastructure.Workers;
using Refahi.Shared.Extensions;
using Refahi.Shared.Infrastructure;

namespace Refahi.Modules.Entertainments.Infrastructure;

public static class DI
{
    public static IServiceCollection RegisterInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<EntertainmentDbContext>(options => options.UseNpgsql(configuration.GetConnectionString(),
            x => x.MigrationsHistoryTable("__EFMigrationsHistory", EntertainmentDbContext.Schema)));
        
        services.Configure<Refahi.Modules.Entertainments.Application.Contracts.Discovery.EntertainmentsOptions>(configuration.GetSection("Entertainments"));
        services.AddScoped<Refahi.Modules.Entertainments.Application.Contracts.Discovery.IEntertainmentDiscovery, Providers.TouristPanel.EntertainmentDiscoveryReader>();
        services.AddDataProtection();
        
        services.AddScoped<IEntertainmentPrivateDataRetention, EntertainmentPrivateDataRetention>();
        services.AddScoped<IEntertainmentRepository, EntertainmentRepository>()
                .AddScoped<IEntertainmentSessionRepository, EntertainmentSessionRepository>()
                .AddSingleton<IEntertainmentMutationLock>(
                    new PostgresEntertainmentMutationLock(configuration.GetConnectionString())
                );

        services.AddScoped<IEntertainmentSecretProtector, EntertainmentSecretProtector>()
                .AddScoped<IEntertainmentProviderFactory, EntertainmentProviderFactory>();

        services.AddTouristPanelProvider(configuration);

        services.AddHostedService<EntertainmentFulfillmentWorker>();
        return services;
    }

    public static void UseInfrastructure(this IServiceProvider provider, bool isDevelopment)
    {
        using var scope = provider.CreateScope();
        scope.ServiceProvider.GetRequiredService<IDbTools>().ApplyMigrations<EntertainmentDbContext>();
    }
}
