using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Refahi.Modules.Organizations.Infrastructure;

public static class DI
{
    public static IServiceCollection RegisterInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        //var connectionStringName = configuration.GetValue<string>("ConnectionStringName");
        //var connectionString = configuration.GetConnectionString(connectionStringName);

        return services;
    }
}