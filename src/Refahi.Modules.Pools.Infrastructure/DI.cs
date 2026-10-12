using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Polly;
using Refahi.Modules.Pools.Application.Contracts;
using Refahi.Modules.Pools.Domain;
using Refahi.Modules.Pools.Infrastructure.Persistence;
using Refahi.Modules.Pools.Infrastructure.Providers;
using Refahi.Modules.Pools.Infrastructure.Providers.PoolTicket;
using Refahi.Modules.Pools.Infrastructure.Workers;
using Refahi.Shared.Extensions;
using Refahi.Shared.Infrastructure;

namespace Refahi.Modules.Pools.Infrastructure;
public static class DI
{
    public static IServiceCollection RegisterInfrastructure(this IServiceCollection services, IConfiguration config)
    {
        services.AddOptions<PoolsOptions>().Bind(config.GetSection("Pools")).Validate(x => x.ReservationMinutes is >= 2 and <= 60 && x.WorkerSeconds >= 5
            && x.SafetySeconds >= 60 && x.SafetySeconds < x.ReservationMinutes * 60
            && (!x.PurchaseEnabled || x.CatalogEnabled && x.MockLifecycleEnabled && x.TestFundsConfirmed), "Invalid Pools configuration or test funds not confirmed").ValidateOnStart();
        services.AddOptions<PoolTicketOptions>().Bind(config.GetSection("Pools:PoolTicket"))
            .Validate(x => Uri.TryCreate(x.BaseUrl, UriKind.Absolute, out var u) && u.Scheme == "https" && x.TimeoutSeconds is >= 1 and <= 120
                && x.RequestEncoding is "Form" or "Json" && x.PriceUnit is null or "IRR" or "TOMAN", "Invalid PoolTicket configuration").ValidateOnStart();
        services.Configure<MockPoolOptions>(config.GetSection("Pools:Mock"));
        services.AddDbContext<PoolsDbContext>(x => x.UseNpgsql(config.GetConnectionString(), p => p.MigrationsHistoryTable("__EFMigrationsHistory", PoolsDbContext.Schema)));
        services.AddScoped<IPoolBookingRepository, PoolBookingRepository>(); services.AddScoped<IPoolCatalogStore, PoolCatalogStore>();
        services.AddSingleton<IPoolMutationLock>(new PostgresPoolMutationLock(config.GetConnectionString()));
        services.AddScoped<IPoolPurchasePolicy, PoolPurchasePolicy>();
        if (config.GetValue<bool>("Pools:MockLifecycleEnabled")) services.AddScoped<MockPoolLifecycle>();
        var breaker = Policy<HttpResponseMessage>.Handle<HttpRequestException>().OrResult(x => (int)x.StatusCode >= 500).CircuitBreakerAsync(5, TimeSpan.FromSeconds(30));
        services.AddHttpClient<PoolTicketClient>((sp, http) =>
        {
            var o = sp.GetRequiredService<IOptions<PoolTicketOptions>>().Value;
            http.BaseAddress = new Uri(o.BaseUrl.TrimEnd('/') + "/"); http.Timeout = TimeSpan.FromSeconds(o.TimeoutSeconds);
        }).AddPolicyHandler(breaker);
        services.AddScoped<PoolTicketProvider>(); services.AddScoped<FixturePoolProvider>();
        services.AddScoped<IPoolProvider>(sp =>
        {
            var o = sp.GetRequiredService<IOptions<PoolsOptions>>().Value;
            IPoolProvider read = o.FixtureCatalogEnabled ? sp.GetRequiredService<FixturePoolProvider>() : sp.GetRequiredService<PoolTicketProvider>();
            return o.MockLifecycleEnabled ? new PoolProviderWithMock(read, sp.GetRequiredService<MockPoolLifecycle>(), sp.GetRequiredService<IOptions<PoolsOptions>>()) : read;
        });
        services.AddScoped<IPoolProviderFactory, PoolProviderFactory>(); services.AddHostedService<PoolRecoveryWorker>();
        return services;
    }
    public static void UseInfrastructure(this IServiceProvider provider)
    {
        using var scope = provider.CreateScope(); var o = scope.ServiceProvider.GetRequiredService<IOptions<PoolsOptions>>().Value;
        var env = scope.ServiceProvider.GetRequiredService<IHostEnvironment>();
        if (!PoolPurchasePolicy.IsTestEnvironment(env) && (o.MockLifecycleEnabled || o.FixtureCatalogEnabled || o.PurchaseEnabled))
            throw new InvalidOperationException("Pools mock purchase/fixture configuration is forbidden outside Development/Test/Staging.");
        if (o.CatalogEnabled) scope.ServiceProvider.GetRequiredService<IDbTools>().ApplyMigrations<PoolsDbContext>();
    }
}
