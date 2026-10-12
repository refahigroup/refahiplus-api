using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Refahi.Modules.Pools.Application;
using Refahi.Modules.Pools.Infrastructure;
using Refahi.Shared.Presentation;

namespace Refahi.Modules.Pools.Api;
public static class DI
{
    public static IServiceCollection RegisterPoolsModule(this IServiceCollection services, IConfiguration config)
        => services.RegisterApplication(config).RegisterInfrastructure(config);
    public static WebApplication UsePoolsModule(this WebApplication app, string prefix)
    {
        app.Services.UseInfrastructure(); var group = app.MapGroup(prefix);
        foreach (var type in typeof(DI).Assembly.GetTypes().Where(t => t.IsClass && !t.IsAbstract && typeof(IEndpoint).IsAssignableFrom(t)))
            ((IEndpoint)Activator.CreateInstance(type)!).Map(group);
        return app;
    }
}
