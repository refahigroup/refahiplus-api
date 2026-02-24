using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Refahi.Modules.Orders.Application;
using Refahi.Modules.Orders.Infrastructure;
using Refahi.Shared.Presentation;

namespace Refahi.Modules.Orders.Api;

public static class DI
{
    public static IServiceCollection RegisterOrdersModule(this IServiceCollection services, IConfiguration configuration)
    {
        services
            .RegisterApplication(configuration)
            .RegisterInfrastructure(configuration);

        return services;
    }

    public static WebApplication UseOrdersModule(this WebApplication app, string endPointsPrefix)
    {
        MapEndPoints(app, endPointsPrefix);

        var group = app.MapGroup("/orders");
        group.MapGet("/ping", () => Results.Ok(new { module = "orders" }));

        return app;
    }

    private static void MapEndPoints(this WebApplication app, string endPointsPrefix)
    {
        var assembly = typeof(DI).Assembly;

        var endpointTypes = assembly.GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract && typeof(IEndpoint).IsAssignableFrom(t));

        var group = app.MapGroup(endPointsPrefix);

        foreach (var type in endpointTypes)
        {
            if (Activator.CreateInstance(type) is IEndpoint endpoint)
            {
                endpoint.Map(group);
            }
        }
    }
}
