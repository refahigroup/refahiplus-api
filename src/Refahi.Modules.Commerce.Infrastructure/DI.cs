using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Refahi.Modules.Commerce.Application.Contracts;
using Refahi.Modules.Commerce.Application.Contracts.Providers;
using Refahi.Modules.Commerce.Domain;
using Refahi.Modules.Commerce.Infrastructure.Persistence;
using Refahi.Modules.Commerce.Infrastructure.Providers.Asbsar;
using Refahi.Modules.Commerce.Infrastructure.Workers;
using Refahi.Shared.Extensions;
using Refahi.Shared.Infrastructure;

namespace Refahi.Modules.Commerce.Infrastructure;

public static class DI
{
    public static IServiceCollection RegisterInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<CommerceDbContext>(options => options.UseNpgsql(configuration.GetConnectionString(),
            x => x.MigrationsHistoryTable("__EFMigrationsHistory", CommerceDbContext.Schema)));
        services.AddDataProtection();
        services.AddScoped<ICommerceRepository, CommerceRepository>();
        services.AddScoped<ICommerceSecretProtector, CommerceSecretProtector>();
        services.AddScoped<ICommerceProviderFactory, CommerceProviderFactory>();
        services.AddAabsarProvider(configuration);
        services.AddHostedService<CommerceFulfillmentWorker>();
        return services;
    }

    public static void UseInfrastructure(this IServiceProvider provider, bool isDevelopment)
    {
        using var scope = provider.CreateScope();
        scope.ServiceProvider.GetRequiredService<IDbTools>().ApplyMigrations<CommerceDbContext>();
    }
}
