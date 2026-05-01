using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Refahi.Modules.SupplyChain.Application.Abstractions;
using Refahi.Modules.SupplyChain.Infrastructure.Persistence.Context;
using Refahi.Modules.SupplyChain.Infrastructure.Repositories;
using Refahi.Shared.Extensions;
using Refahi.Shared.Infrastructure;

namespace Refahi.Modules.SupplyChain.Infrastructure;

public static class DI
{
    public static IServiceCollection RegisterInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<SupplyChainDbContext>(options =>
        {
            string connectionString = configuration.GetConnectionString();
            options.UseNpgsql(connectionString);
        });

        services.AddScoped<ISupplierRepository, SupplierRepository>();
        services.AddScoped<IAgreementRepository, AgreementRepository>();

        return services;
    }

    public static void UseInfrastructure(this IServiceProvider provider, bool isDevelopment)
    {
        using var scope = provider.CreateScope();
        var tools = scope.ServiceProvider.GetRequiredService<IDbTools>();
        tools.ApplyMigrations<SupplyChainDbContext>();
    }
}