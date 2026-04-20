using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Refahi.Modules.Store.Application;
using Refahi.Modules.Store.Infrastructure;
using Refahi.Shared.Presentation;

namespace Refahi.Modules.Store.Api;

public static class DI
{
    public static IServiceCollection RegisterStoreModule(this IServiceCollection services, IConfiguration configuration)
    {
        services
            .RegisterApplication(configuration)
            .RegisterInfrastructure(configuration);

        return services;
    }

    public static WebApplication UseStoreModule(this WebApplication app, string endPointsPrefix)
    {
        app.Services.UseInfrastructure(app.Environment.IsDevelopment());

        MapEndPoints(app, endPointsPrefix);

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

        group.MapGet("/ping", () => Results.Ok(new { module = "Store Module" }));
    }
}