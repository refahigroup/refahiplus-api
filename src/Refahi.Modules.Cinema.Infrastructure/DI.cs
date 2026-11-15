using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Refahi.Modules.Catalogue.Application.Contracts;
using Refahi.Modules.Cinema.Infrastructure.Providers.iTicket;
using Refahi.Modules.Cinema.Infrastructure.Persistence;
using Refahi.Modules.Cinema.Infrastructure.Workers;
using Refahi.Modules.Cinema.Infrastructure.Printing;
using Refahi.Modules.Cinema.Application.Contracts;
using Refahi.Modules.Cinema.Domain;
using Refahi.Shared.Extensions;
using Refahi.Shared.Infrastructure;
namespace Refahi.Modules.Cinema.Infrastructure;
public static class DI
{
    public static IServiceCollection RegisterInfrastructure(this IServiceCollection services,IConfiguration configuration)
    {
        services.AddDbContext<CinemaDbContext>(x=>x.UseNpgsql(configuration.GetConnectionString(),
            p=>p.MigrationsHistoryTable("__EFMigrationsHistory",CinemaDbContext.Schema)));
        services.Configure<CinemaProviderSettings>(configuration.GetSection("Cinema:iTicket"));
        services.AddMemoryCache();services.AddiTicketClient(configuration);
        services.AddScoped<ITicketCinemaProvider>();
        services.AddScoped<ICinemaProvider>(sp => sp.GetRequiredService<ITicketCinemaProvider>());
        services.AddScoped<ICinemaProviderFactory,CinemaProviderFactory>();
        services.TryAddSingleton(TimeProvider.System);
        services.AddOptions<CinemaDiscoveryOptions>().Bind(configuration.GetSection("Cinema:Discovery"))
            .Validate(x => x.RefreshMinutes > 0 && x.RefreshMinutes <= x.StaleMinutes
                && x.StaleMinutes < x.MaxAgeMinutes && x.MaxAgeMinutes <= 1440 && x.HorizonDays is > 0 and <= 14,
                "Cinema discovery freshness and horizon must be valid.").ValidateOnStart();
        services.AddScoped<ICinemaScheduleInventory, ITicketScheduleInventory>();
        services.AddScoped<ICinemaDiscoverySync, ITicketDiscoverySync>();
        services.AddScoped<CinemaCatalogueSource>();
        services.AddScoped<ICinemaDiscoveryReader>(sp => sp.GetRequiredService<CinemaCatalogueSource>());
        if (configuration.GetValue<bool>("Cinema:CatalogEnabled")
            && configuration.GetValue<bool>("Cinema:Discovery:CatalogueEnabled"))
            services.AddSingleton(new CatalogueSourceRegistration("cinema", typeof(CinemaCatalogueSource)));
        services.AddScoped<ICinemaOrderRepository,CinemaOrderRepository>();
        services.AddSingleton<ICinemaMutationLock>(new PostgresCinemaMutationLock(configuration.GetConnectionString()));
        services.AddScoped<ICinemaTicketRenderer,CinemaTicketRenderer>();
        services.AddHostedService<CinemaReconciliationWorker>();
        services.AddHostedService<CinemaDiscoveryWorker>();
        return services;
    }
    public static void UseInfrastructure(this IServiceProvider provider,bool isDevelopment)
    {
        using var scope=provider.CreateScope();scope.ServiceProvider.GetRequiredService<IDbTools>().ApplyMigrations<CinemaDbContext>();
    }
}
