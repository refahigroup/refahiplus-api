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
        this IServiceCollection services,
        IConfiguration configuration,
        bool isDevelopment = false
    )
    {
        services.AddOptions<PersistentExternalMediaOptions>().Bind(configuration.GetSection("PersistentExternalMedia"))
            .Validate(x => !x.Enabled || (!string.IsNullOrWhiteSpace(x.StoragePath) && x.MinFreeBytes >= 0
                && !PathsOverlap(x.StoragePath, configuration["ExternalMedia:CachePath"])), "مسیر ذخیره پایدار مدیا معتبر نیست.").ValidateOnStart();
        services.AddSingleton<PersistentExternalMedia>();
        services.AddSingleton<Refahi.Modules.Media.Application.Contracts.External.IPersistentExternalMedia>(sp => sp.GetRequiredService<PersistentExternalMedia>());
        services.AddOptions<ExternalMediaOptions>().Bind(configuration.GetSection("ExternalMedia"))
            .Validate(x => Uri.TryCreate(x.PublicBaseUrl, UriKind.Absolute, out var u) && (u.Scheme == "https" || u.IsLoopback)
                && x.MaxImageBytes > 0 && x.MaxSourceBytes >= x.MaxImageBytes
                && x.MaxSourceBytes <= 100 * 1024 * 1024 && x.MaxBytes > (long)x.MaxSourceBytes + x.MaxImageBytes + 8192
                && x.MaxImageDimension is > 0 and <= 1600 && x.MaxSourcePixels is > 0 and <= 40_000_000
                && x.ConcurrentDownloads is > 0 and <= 16
                && x.QueueLimit > 0 && x.MaxEntries > 0 && x.MinFreeBytes >= 0 && x.FreshHours > 0 && x.SlidingExpirationHours > 0 && x.ActivityFlushMinutes > 0 && x.CleanupMinutes > 0 && x.QueueWaitSeconds > 0 && x.ConnectSeconds > 0 && x.DownloadSeconds > 0 && x.NegativeSeconds > 0 && !string.IsNullOrWhiteSpace(x.CachePath) && !PathsOverlap(x.CachePath, configuration["MediaStorage:BasePath"]), "تنظیمات کش مدیا معتبر نیست.").ValidateOnStart();
        services.AddScoped<ExternalMediaRegistry>();
        services.AddScoped<Refahi.Modules.Media.Application.Contracts.External.IExternalMediaService>(sp => sp.GetRequiredService<ExternalMediaRegistry>());
        services.AddScoped<Refahi.Modules.Media.Application.Contracts.External.IExternalMediaResolver>(sp => sp.GetRequiredService<ExternalMediaRegistry>());
        services.AddSingleton<ExternalMediaActivity>();
        services.AddSingleton<IExternalMediaActivity>(sp => sp.GetRequiredService<ExternalMediaActivity>());
        services.AddHostedService(sp => sp.GetRequiredService<ExternalMediaActivity>());
        services.AddSingleton<ExternalMedia>();
        services.AddSingleton<Refahi.Modules.Media.Application.Contracts.External.IExternalMediaUrls>(sp => sp.GetRequiredService<ExternalMedia>());
        services.AddHostedService(sp => sp.GetRequiredService<ExternalMedia>());
        services.Configure<MediaStorageOptions>(
            configuration.GetSection(MediaStorageOptions.Section)
        );

        services.AddDbContext<MediaDbContext>(options =>
        {
            string connectionString = configuration.GetConnectionString();

            options.UseNpgsql(
                connectionString,
                npgsqlOptions =>
                {
                    npgsqlOptions.MigrationsHistoryTable("__EFMigrationsHistory", "media");
                }
            );
        });

        services.AddScoped<IMediaAssetRepository, MediaAssetRepository>();
        services.AddHttpClient("MediaPublicRead", client =>
            client.Timeout = TimeSpan.FromSeconds(3)
        );
        services.AddSingleton<IMediaStorageService>(provider =>
            new FileSystemMediaStorageService(
                provider.GetRequiredService<Microsoft.Extensions.Options.IOptions<MediaStorageOptions>>(),
                provider.GetRequiredService<IHttpClientFactory>().CreateClient("MediaPublicRead")
            )
        );
        services.AddSingleton<IMediaContentValidator, MagicBytesContentValidator>();

        return services;
    }

    private static bool PathsOverlap(string first, string? second)
    {
        if (string.IsNullOrWhiteSpace(second)) return false;
        var a = Path.GetFullPath(first).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var b = Path.GetFullPath(second).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        return a.StartsWith(b, comparison) || b.StartsWith(a, comparison);
    }
    public static void UseInfrastructure(this IServiceProvider provider, bool isDevelopment)
    {
        using var scope = provider.CreateScope();
        var tools = scope.ServiceProvider.GetRequiredService<IDbTools>();
        tools.ApplyMigrations<MediaDbContext>();
    }
}
