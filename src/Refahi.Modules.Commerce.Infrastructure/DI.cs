using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Refahi.Shared.Infrastructure;
using Refahi.Shared.Extensions;

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

        return services;
    }

    public static void UseInfrastructure(this IServiceProvider provider, bool isDevelopment)
    {
        using var scope = provider.CreateScope();

        //scope.ServiceProvider.GetRequiredService<IDbTools>().ApplyMigrations<CommerceDbContext>();
    }
}
