using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Refahi.Modules.Hotels.Application.Contracts.Providers;
using Refahi.Modules.Hotels.Domain.Abstraction.Repositories;
using Refahi.Modules.Hotels.Infrastructure.Config;
using Refahi.Modules.Hotels.Infrastructure.Extensions;
using Refahi.Modules.Hotels.Infrastructure.Persistence;
using Refahi.Modules.Hotels.Infrastructure.Persistence.Repositories;
using Refahi.Modules.Hotels.Infrastructure.Providers;
using Refahi.Modules.Hotels.Infrastructure.Providers.SnappTrip;
using Refahi.Modules.Hotels.Infrastructure.Providers.SnappTrip.Config;
using Refahi.Shared.Extensions;

namespace Refahi.Modules.Hotels.Infrastructure;

public static class DI
{
    public static IServiceCollection AddHotelsInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        string connectionString = configuration.GetConnectionString();

        var hotelsConfig = configuration.GetSection("Refahi:Hotels");
        //var connectionString = hotelsConfig.GetValue<string>("ConnectionString");

        // DbContext
        services.AddDbContext<HotelsDbContext>(options =>
        {
            var connectionStringName = configuration.GetValue<string>("ConnectionStringName");
            var connectionString = configuration.GetConnectionString(connectionStringName);

            options.UseNpgsql(connectionString);
        });

        // Repositories
        services.AddScoped<IBookingRepository, BookingRepository>();

        // Providers
        services.UseSnappTripProvider(configuration)
                .AddScoped<IHotelProvider>(sp => sp.GetRequiredService<IHotelProviderFactory>().GetDefaultProvider())
                .AddScoped<IHotelProviderFactory>(sp => new HotelProviderFactory(sp, HotelProviderType.SnappTrip));

        return services;
    }

    public static void UseHotelInfrastructure(this IServiceProvider provider, bool isDev)
    {
         if (isDev)
        {
            provider.ApplyPendingMigrations<HotelsDbContext>();
        }
    }
}
