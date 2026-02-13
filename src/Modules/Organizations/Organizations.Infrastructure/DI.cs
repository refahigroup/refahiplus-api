using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Organizations.Infrastructure;

public static class DI
{
    public static IServiceCollection RegisterInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        return services;
    }
}