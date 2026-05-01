using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Refahi.Modules.Media.Application.Services;
using Refahi.Modules.Media.Domain.Repositories;
using Refahi.Modules.Media.Infrastructure.Options;
using Refahi.Modules.Media.Infrastructure.Persistence.Context;
using Refahi.Modules.Media.Infrastructure.Repositories;
using Refahi.Modules.Media.Infrastructure.Services;
using Refahi.Shared.Extensions;
using Refahi.Shared.Infrastructure;

namespace Refahi.Modules.Media.Infrastructure;

public static class DI
{
    public static IServiceCollection RegisterInfrastructure(
        this IServiceCollection services, IConfiguration configuration, bool isDevelopment = false)
    {
        services.Configure<MediaStorageOptions>(
            configuration.GetSection(MediaStorageOptions.Section));

        services.AddDbContext<MediaDbContext>(options =>
        {
            string connectionString = configuration.GetConnectionString();

            options.UseNpgsql(connectionString, npgsqlOptions =>
            {
                npgsqlOptions.MigrationsHistoryTable("__EFMigrationsHistory", "media");
            });
        });

        services.AddScoped<IMediaAssetRepository, MediaAssetRepository>();
        services.AddSingleton<IMediaStorageService, FileSystemMediaStorageService>();
        services.AddSingleton<IMediaContentValidator, MagicBytesContentValidator>();

        return services;
    }

    public static void UseInfrastructure(this IServiceProvider provider, bool isDevelopment)
    {
        using var scope = provider.CreateScope();
        var tools = scope.ServiceProvider.GetRequiredService<IDbTools>();
        tools.ApplyMigrations<MediaDbContext>();
    }
}
