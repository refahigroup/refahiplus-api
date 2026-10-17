using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Polly;
using Refahi.Modules.Pools.Application.Contracts;
using Refahi.Modules.Pools.Application.Contracts.Public;
using Refahi.Modules.Pools.Domain;
using Refahi.Modules.Pools.Infrastructure.Persistence;
using Refahi.Modules.Pools.Infrastructure.Providers;
using Refahi.Modules.Pools.Infrastructure.Providers.PoolTicket;
using Refahi.Modules.Pools.Infrastructure.Workers;
using Refahi.Modules.Pools.Infrastructure.Security;
using Refahi.Modules.Pools.Infrastructure.Printing;
using Refahi.Shared.Extensions;
using Refahi.Shared.Infrastructure;
namespace Refahi.Modules.Pools.Infrastructure;
public static class DI
{
    public static IServiceCollection RegisterInfrastructure(this IServiceCollection services, IConfiguration config)
    {
        services.AddOptions<PoolsOptions>().Bind(config.GetSection("Pools"))
            .Validate(x => x.ReservationMinutes is >= 2 and <= 60 && x.WorkerSeconds >= 5 && x.VenueSyncSeconds >= 5
                && x.SessionSyncSeconds >= 5 && x.SyncConcurrency is >= 1 and <= 8 && x.MaxCatalogPages is >= 1 and <= 100000
                && (!x.PurchaseEnabled || x.CatalogEnabled), "تنظیمات استخر معتبر نیست").ValidateOnStart();
        services.AddOptions<PoolTicketOptions>().Bind(config.GetSection("Pools:PoolTicket"))
            .Validate(x => Uri.TryCreate(x.BaseUrl, UriKind.Absolute, out var u) && u.Scheme == "https" && x.TimeoutSeconds is >= 1 and <= 120
                && x.RequestEncoding is "Form" or "Json" && x.PriceUnit == "TOMAN", "تنظیمات اتصال استخر معتبر نیست").ValidateOnStart();
        services.AddDbContext<PoolsDbContext>(x => x.UseNpgsql(config.GetConnectionString(), p => p.MigrationsHistoryTable("__EFMigrationsHistory", PoolsDbContext.Schema)));
        services.AddScoped<IPoolPurchaseRepository, PoolPurchaseRepository>().AddScoped<IPoolCatalogRepository, PoolCatalogRepository>()
            .AddScoped<IPoolCatalogReader, PoolCatalogReader>().AddScoped<IPoolCatalogDiagnostics, PoolCatalogDiagnostics>().AddScoped<ILegacyPoolStore, LegacyPoolStore>();
        services.AddSingleton<IPoolMutationLock>(new PostgresPoolMutationLock(config.GetConnectionString()));
        var protection = services.AddDataProtection();
        if (!string.IsNullOrWhiteSpace(config["DataProtection:KeysPath"]))
            protection.PersistKeysToFileSystem(new DirectoryInfo(config["DataProtection:KeysPath"]!));
        services.AddScoped<IPoolCodeProtector, PoolCodeProtector>();
        services.AddScoped<IPoolVoucherRenderer, PoolVoucherRenderer>().AddSingleton<IPoolSessionTimeParser, PoolSessionTimeParser>()
            .AddSingleton<IPoolReservationGateway, PoolTicketReservationGateway>();
        var breaker = Policy<HttpResponseMessage>.Handle<HttpRequestException>().OrResult(x => (int)x.StatusCode >= 500)
            .CircuitBreakerAsync(5, TimeSpan.FromSeconds(30));
        services.AddHttpClient<PoolTicketClient>((sp, http) =>
        {
            var o = sp.GetRequiredService<IOptions<PoolTicketOptions>>().Value;
            http.BaseAddress = new Uri(o.BaseUrl.TrimEnd('/') + "/"); http.Timeout = TimeSpan.FromSeconds(o.TimeoutSeconds);
        }).ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false })
            .AddPolicyHandler(breaker);
        services.AddScoped<IPoolProvider, PoolTicketProvider>().AddScoped<IPoolProviderFactory, PoolProviderFactory>();
        services.AddHostedService<PoolCatalogWorker>().AddHostedService<PoolRecoveryWorker>();
        return services;
    }
    public static void UseInfrastructure(this IServiceProvider provider)
    {
        using var scope = provider.CreateScope();
        if (scope.ServiceProvider.GetRequiredService<IOptionsMonitor<PoolsOptions>>().CurrentValue.CatalogEnabled)
            scope.ServiceProvider.GetRequiredService<IDbTools>().ApplyMigrations<PoolsDbContext>();
    }
}
