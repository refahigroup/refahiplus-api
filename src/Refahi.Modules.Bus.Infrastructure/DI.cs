using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Refahi.Modules.Bus.Application.Contracts;
using Refahi.Modules.Bus.Domain.Discovery;
using Refahi.Modules.Bus.Infrastructure.Persistence;
using Refahi.Modules.Bus.Infrastructure.Providers.HiHoliday;
using Refahi.Shared.Extensions;
using Refahi.Shared.Infrastructure;

namespace Refahi.Modules.Bus.Infrastructure;

public static class DI
{
    public static IServiceCollection RegisterBusInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<BusDbContext>(options => options.UseNpgsql(configuration.GetConnectionString(),
            npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "bus")));
        services.AddScoped<IBusDiscoveryRepository, BusDiscoveryRepository>();
        services.AddMemoryCache();
        services.AddScoped<IBusProvider, HiHolidayProvider>();

        services.Configure<HiHolidayOptions>(configuration.GetSection("Bus:HiHoliday"));
        services.AddHttpClient<HiHolidayClient>()
            .RemoveAllLoggers()
            .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
            {
                AllowAutoRedirect = false,
                ActivityHeadersPropagator = null
            });
        return services;
    }

    public static void MigrateBus(this IServiceProvider services)
    {
        using var scope = services.CreateScope();
        scope.ServiceProvider.GetRequiredService<IDbTools>().ApplyMigrations<BusDbContext>();
    }
}
