using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Refahi.Modules.Cinema.Infrastructure.Providers.iTicket;
using Refahi.Shared.Extensions;

namespace Refahi.Modules.Cinema.Infrastructure;

public static class DI
{
    public static IServiceCollection RegisterInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration
    )
    {
        var connectionString = configuration.GetConnectionString();

        //services.AddDbContext<CinemaDbContext>(options =>
        //    options.UseNpgsql(
        //        connectionString,
        //        npgsql =>
        //            npgsql.MigrationsHistoryTable("__EFMigrationsHistory", ChargeDbContext.Schema)
        //    )
        //);


        services.AddiTicketClient(configuration);

        services.AddDataProtection();

        return services;
    }

    public static void UseInfrastructure(this IServiceProvider provider, bool isDevelopment)
    {
        using var scope = provider.CreateScope();

        //scope.ServiceProvider
        //     .GetRequiredService<IDbTools>()
        //     .ApplyMigrations<CinemaDbContext>();
    }
}
