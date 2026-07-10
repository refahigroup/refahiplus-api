using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Refahi.Modules.Charge.Application.Contracts.Providers;
using Refahi.Modules.Charge.Domain.Repositories;
using Refahi.Modules.Charge.Infrastructure.Persistence.Context;
using Refahi.Modules.Charge.Infrastructure.Persistence.Repositories;
using Refahi.Modules.Charge.Infrastructure.Providers;
using Refahi.Modules.Charge.Infrastructure.Providers.Eniac;
using Refahi.Modules.Charge.Infrastructure.Workers;
using Refahi.Shared.Extensions;
using Refahi.Shared.Infrastructure;

namespace Refahi.Modules.Charge.Infrastructure;

public static class DI
{
    public static IServiceCollection RegisterInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString();

        services.AddDbContext<ChargeDbContext>(options => options.UseNpgsql(connectionString,
            npgsql => npgsql.MigrationsHistoryTable("__EFMigrationsHistory", ChargeDbContext.Schema)));

        services.AddScoped<IChargeRequestRepository, ChargeRequestRepository>()
                .AddScoped<IChargeMarkupRuleRepository, ChargeMarkupRuleRepository>()
                .AddScoped<IChargeSecretProtector, ChargeSecretProtector>();

        services.AddDataProtection();

        services.Configure<EniacOptions>(configuration.GetSection(EniacOptions.Section));
        services.AddHttpClient<EniacApiClient>((sp, http) =>
        {
            var options = sp.GetRequiredService<IOptions<EniacOptions>>().Value;
            http.BaseAddress = new Uri(string.IsNullOrWhiteSpace(options.BaseUrl) ? "https://localhost/" : options.BaseUrl);
            http.Timeout = TimeSpan.FromSeconds(Math.Clamp(options.TimeoutSeconds, 5, 120));
        });

        services.AddScoped<IChargeProvider, EniacChargeProvider>();

        services.AddScoped<IChargeProviderResolver, ChargeProviderResolver>();
        services.AddHostedService<ChargeFulfillmentWorker>();

        return services;
    }

    public static void UseInfrastructure(this IServiceProvider provider, bool isDevelopment)
    {
        using var scope = provider.CreateScope();

        scope.ServiceProvider
             .GetRequiredService<IDbTools>()
             .ApplyMigrations<ChargeDbContext>();
    }
}
