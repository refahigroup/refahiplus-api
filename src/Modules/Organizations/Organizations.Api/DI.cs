using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Organizations.Application;
using Organizations.Infrastructure;

namespace Organizations.Api;

public static class DI
{
    public static IServiceCollection RegisterOrganizationsModule(this IServiceCollection services, IConfiguration configuration)
    {
        services
            .RegisterApplication(configuration)
            .RegisterInfrastructure(configuration);

        return services;
    }

    public static WebApplication MapOrganizationsEndpoints(this WebApplication app, string endPointsPrefix)
    {
        MapEndPoints(app, endPointsPrefix);

        var group = app.MapGroup("/organizations");
        group.MapGet("/ping", () => Results.Ok(new { module = "organizations" }));

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
