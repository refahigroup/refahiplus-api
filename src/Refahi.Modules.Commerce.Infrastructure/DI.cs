using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Refahi.Modules.Commerce.Application.Contracts.Abstraction;
using Refahi.Modules.Commerce.Infrastructure.Providers.Asbsar;
using Refahi.Shared.Extensions;
using Refahi.Shared.Infrastructure;

namespace Refahi.Modules.Commerce.Infrastructure;

public static class DI
{
    public static IServiceCollection RegisterInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString();

        //services.AddDbContext<CommerceDbContext>(options =>
        //    options.UseNpgsql(
        //        connectionString,
        //        npgsql =>
        //            npgsql.MigrationsHistoryTable("__EFMigrationsHistory", CommerceDbContext.Schema)
        //    )
        //);

        services.AddAabsarProvider(configuration);

        return services;
    }

    public static void UseInfrastructure(this IServiceProvider provider, bool isDevelopment)
    {
        using var scope = provider.CreateScope();

        //scope.ServiceProvider.GetRequiredService<IDbTools>().ApplyMigrations<CommerceDbContext>();


        provider.UseAabsarProvider();


    }
}
