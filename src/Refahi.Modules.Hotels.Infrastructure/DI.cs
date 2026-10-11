using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Polly;
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
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration
    )
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
        services.AddScoped<
            IHotelProviderBookingCacheRepository,
            HotelProviderBookingCacheRepository
        >();

        services.AddScoped<IHotelCatalogueRepository, HotelCatalogueRepository>();
        services.AddMemoryCache();
        services.AddSingleton<Providers.Catalogue.HotelInventoryStore>();
        services.Configure<Providers.Lamasoo.LamasooOptions>(configuration.GetSection("Hotels:Providers:Lamasoo"));
        services.PostConfigure<Providers.Lamasoo.LamasooOptions>(o => o.ApiKey = o.ApiKey.ReplaceWithEnvironmentVariables());
        services.AddHttpClient<Providers.Lamasoo.LamasooApiClient>((sp, client) =>
        {
            var o = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<Providers.Lamasoo.LamasooOptions>>().Value;
            if (!Uri.TryCreate(o.BaseUrl, UriKind.Absolute, out var uri) || uri.Scheme != "https") throw new InvalidOperationException("نشانی لاماسو معتبر نیست.");
            client.BaseAddress = uri; client.Timeout = TimeSpan.FromSeconds(Math.Clamp(o.TimeoutSeconds, 1, 60));
            client.DefaultRequestHeaders.TryAddWithoutValidation("Authorization", o.ApiKey);
            if (o.IncludeAgencyProvider) client.DefaultRequestHeaders.Add("x-include-agency-provider", "true");
        }).AddPolicyHandler(Polly.Extensions.Http.HttpPolicyExtensions.HandleTransientHttpError().CircuitBreakerAsync(5, TimeSpan.FromSeconds(30)));
        services.AddScoped<Providers.Lamasoo.LamasooHotelProvider>();
        services.AddScoped<Providers.Catalogue.SnappTripCatalogueProvider>();
        services.AddScoped<Refahi.Modules.Hotels.Application.Contracts.Catalogue.IHotelCatalogueProviderRegistry, Providers.Catalogue.HotelCatalogueProviderRegistry>();
        services.AddHostedService<Workers.HotelCatalogueRecoveryWorker>();

        // Providers
        services
            .UseSnappTripProvider(configuration)
            .AddScoped<IHotelProvider>(sp =>
                sp.GetRequiredService<IHotelProviderFactory>().GetDefaultProvider()
            )
            .AddScoped<IHotelProviderFactory>(sp => new HotelProviderFactory(
                sp,
                HotelProviderType.SnappTrip
            ));

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
