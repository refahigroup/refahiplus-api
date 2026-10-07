using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
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
        services.AddScoped<ICinemaProvider,ITicketCinemaProvider>();services.AddScoped<ICinemaProviderFactory,CinemaProviderFactory>();
        services.AddScoped<ICinemaOrderRepository,CinemaOrderRepository>();
        services.AddSingleton<ICinemaMutationLock>(new PostgresCinemaMutationLock(configuration.GetConnectionString()));
        services.AddScoped<ICinemaTicketRenderer,CinemaTicketRenderer>();
        services.AddHostedService<CinemaReconciliationWorker>();return services;
    }
    public static void UseInfrastructure(this IServiceProvider provider,bool isDevelopment)
    {
        using var scope=provider.CreateScope();scope.ServiceProvider.GetRequiredService<IDbTools>().ApplyMigrations<CinemaDbContext>();
    }
}
