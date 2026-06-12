using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Refahi.Modules.Flights.Application.Contracts.Providers;
using Refahi.Modules.Flights.Domain.Repositories;
using Refahi.Modules.Flights.Infrastructure.Persistence;
using Refahi.Modules.Flights.Infrastructure.Persistence.Repositories;
using Refahi.Modules.Flights.Infrastructure.Providers;
using Refahi.Modules.Flights.Infrastructure.Providers.SnappTrip;
using Refahi.Shared.Extensions;
using Refahi.Shared.Infrastructure;

namespace Refahi.Modules.Flights.Infrastructure;

public static class DI
{
    public static IServiceCollection RegisterInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        string connectionString = configuration.GetConnectionString();

        services.AddDbContext<FlightsDbContext>(options =>
            options.UseNpgsql(
                connectionString,
                npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory", "flights")));

        services.AddScoped<IFlightBookingRepository, FlightBookingRepository>();
        services.AddScoped<IFlightOfferSnapshotRepository, FlightOfferSnapshotRepository>();
        services.UseSnappTripFlightProvider(configuration)
            .AddScoped<IFlightProvider>(sp => sp.GetRequiredService<IFlightProviderFactory>().GetDefaultProvider())
            .AddScoped<IFlightProviderFactory>(sp => new FlightProviderFactory(sp, FlightProviderType.SnappTrip));

        return services;
    }

    public static void UseInfrastructure(this IServiceProvider provider, bool isDevelopment)
    {
        using var scope = provider.CreateScope();
        var tools = scope.ServiceProvider.GetRequiredService<IDbTools>();

        tools.ApplyMigrations<FlightsDbContext>();
    }
}
