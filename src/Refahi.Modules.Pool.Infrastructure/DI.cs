using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Refahi.Modules.Pool.Infrastructure.Providers.Aabsar;

namespace Refahi.Modules.Pool.Infrastructure;

public static class DI
{
    public static IServiceCollection RegisterInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        //services.AddDbContext<OrganizationsDbContext>(options =>
        //{
        //    string connectionString = configuration.GetConnectionString();

        //    options.UseNpgsql(connectionString);
        //});

        //services.AddAabsarProvider(configuration);

        return services;
    }

    public static void UseInfrastructure(this IServiceProvider provider, bool IsDevelopment)
    {
        //if (!IsDevelopment)
        //    return;

        //using var scope = provider.CreateScope();
        //var tools = scope.ServiceProvider.GetRequiredService<IDbTools>();

        //tools.ApplyMigrations<OrganizationsDbContext>();
    }
}