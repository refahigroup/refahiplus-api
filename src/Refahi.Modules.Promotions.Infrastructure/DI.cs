using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Refahi.Modules.Promotions.Domain;
using Refahi.Shared.Extensions;
using Refahi.Shared.Infrastructure;
namespace Refahi.Modules.Promotions.Infrastructure;

public static class DI
{
    public static IServiceCollection RegisterInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<PromotionDbContext>(o => o.UseNpgsql(configuration.GetConnectionString()));
        services.AddScoped<IPromotionStore, PromotionStore>();
        services.AddDataProtection(); services.AddSingleton<Refahi.Modules.Promotions.Application.Contracts.IPromotionCodeProtector, PromotionCodeProtector>();
        return services;
    }
    public static void UseInfrastructure(this IServiceProvider provider, bool development) { using var scope = provider.CreateScope(); scope.ServiceProvider.GetRequiredService<IDbTools>().ApplyMigrations<PromotionDbContext>(); }
}
