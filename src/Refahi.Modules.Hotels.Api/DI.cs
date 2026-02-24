using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Refahi.Modules.Hotels.Application;
using Refahi.Modules.Hotels.Infrastructure;
using Refahi.Shared.Presentation;

namespace Refahi.Modules.Hotels.Api;

public static class DI
{
    public static IServiceCollection RegisterHotelsModule(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddHotelsInfrastructure(configuration);
        services.AddHotelsApplication(configuration);

        return services;
    }

    public static IApplicationBuilder UseHotelModule(this WebApplication app, string endPointsPrefix)
    {
        app.Services.UseHotelInfrastructure(app.Environment.IsDevelopment());

        MapEndPoints(app, endPointsPrefix);

        return app;
    }

    private static void MapEndPoints(WebApplication app, string endPointsPrefix)
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
