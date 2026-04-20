using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Refahi.Modules.References.Domain.Repositories;
using Refahi.Modules.References.Infrastructure.Persistence.Context;
using Refahi.Modules.References.Infrastructure.Repositories;
using Refahi.Shared.Extensions;
using Refahi.Shared.Infrastructure;

namespace Refahi.Modules.References.Infrastructure;

public static class DI
{
    public static IServiceCollection RegisterInfrastructure(this IServiceCollection services, IConfiguration configuration, bool isDevelopment = false)
    {
        services.AddDbContext<ReferencesDbContext>(options =>
        {
            string connectionString = configuration.GetConnectionString();

            options.UseNpgsql(connectionString, npgsqlOptions =>
            {
                npgsqlOptions.MigrationsHistoryTable("__EFMigrationsHistory", "references");
            });
        });

        services.AddScoped<IProvinceRepository, ProvinceRepository>();
        services.AddScoped<ICityRepository, CityRepository>();

        return services;
    }

    public static void UseInfrastructure(this IServiceProvider provider, bool isDevelopment)
    {
        //if (!IsDevelopment)
        //    return;

        using var scope = provider.CreateScope();
        var tools = scope.ServiceProvider.GetRequiredService<IDbTools>();

        tools.ApplyMigrations<ReferencesDbContext>();

    }
}