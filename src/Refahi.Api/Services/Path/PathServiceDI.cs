using Refahi.Shared.Services.Path;

namespace Refahi.Api.Services.Path;

public static class PathServiceDI
{
    public static IServiceCollection RegisterPathService(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<IPathService, PathService>();

        return services;
    }
}
