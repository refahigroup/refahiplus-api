using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Refahi.Modules.Hotels.Application.Contracts.Providers;
using Refahi.Modules.Hotels.Domain.Abstraction.Repositories;
using Refahi.Modules.Hotels.Infrastructure.Persistence;
using Refahi.Modules.Hotels.Infrastructure.Persistence.Repositories;
using Refahi.Modules.Hotels.Infrastructure.Providers;
using Refahi.Modules.Hotels.Infrastructure.Providers.SnappTrip;
using Refahi.Modules.Hotels.Infrastructure.Workers;
using Refahi.Shared.Extensions;
using Refahi.Shared.Infrastructure;

namespace Refahi.Modules.Hotels.Infrastructure;

public static class DI
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var hotelsConfig = configuration.GetSection("Refahi:Hotels");

        // DbContext
        services.AddDbContext<HotelsDbContext>(options =>
        {
            string connectionString = configuration.GetConnectionString();

            options.UseNpgsql(connectionString);
        });

        // Repositories
        services.AddScoped<IBookingRepository, BookingRepository>();
        services.AddScoped<IHotelRequestRepository, HotelRequestRepository>();
        services.AddScoped<IHotelBookingSagaRepository, HotelBookingSagaRepository>();
        services.AddScoped<IHotelProviderBookingCacheRepository, HotelProviderBookingCacheRepository>();

        // Providers
        services.UseSnappTripProvider(configuration)
                .AddScoped<IHotelProvider>(sp => sp.GetRequiredService<IHotelProviderFactory>().GetDefaultProvider())
                .AddScoped<IHotelProviderFactory>(sp => new HotelProviderFactory(sp, HotelProviderType.SnappTrip));

        services.AddHostedService<HotelProviderReconciliationWorker>();
        services.AddHostedService<HotelSagaRecoveryWorker>();

        return services;
    }

    public static void UseInfrastructure(this IServiceProvider provider, bool IsDevelopment)
    {
        //if (!IsDevelopment)
        //    return;

        using var scope = provider.CreateScope();
        var tools = scope.ServiceProvider.GetRequiredService<IDbTools>();

        tools.ApplyMigrations<HotelsDbContext>();

    }
}
