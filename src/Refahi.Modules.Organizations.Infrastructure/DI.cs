using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Refahi.Modules.Organizations.Domain;
using Refahi.Shared.Extensions;
using Refahi.Shared.Infrastructure;
namespace Refahi.Modules.Organizations.Infrastructure;

public static class DI
{
    public static IServiceCollection RegisterInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<OrganizationDbContext>(o => o.UseNpgsql(configuration.GetConnectionString()));
        services.AddScoped<IOrganizationStore, OrganizationStore>();

        return services;
    }
    public static void UseInfrastructure(this IServiceProvider provider, bool development) { using var scope = provider.CreateScope(); scope.ServiceProvider.GetRequiredService<IDbTools>().ApplyMigrations<OrganizationDbContext>(); }
}
