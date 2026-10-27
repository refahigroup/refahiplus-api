using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Refahi.Modules.Entertainments.Application;
using Refahi.Modules.Entertainments.Infrastructure;
using Refahi.Shared.Presentation;

namespace Refahi.Modules.Entertainments.Api;

public static class DI
{
    public static IServiceCollection RegisterEntertainmentsModule(this IServiceCollection services, IConfiguration configuration)
    {
        services.RegisterApplication(configuration)
                .RegisterInfrastructure(configuration);

        return services;
    }

    public static WebApplication UseEntertainmentsModule(this WebApplication app, string endPointsPrefix)
    {
        app.Services.UseInfrastructure(app.Environment.IsDevelopment());

        MapEndPoints(app, endPointsPrefix);

        return app;
    }

    private static void MapEndPoints(this WebApplication app, string endPointsPrefix)
    {
        var assembly = typeof(DI).Assembly;

        var endpointTypes = assembly
            .GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract && typeof(IEndpoint).IsAssignableFrom(t));

        var group = app.MapGroup(endPointsPrefix).AddEndpointFilter(async (context, next) => {
            context.HttpContext.Response.Headers.CacheControl = "no-store";
            var relative = context.HttpContext.Request.Path.Value![endPointsPrefix.Length..];
            var continuation = relative.StartsWith("/purchases/", StringComparison.Ordinal) || relative.StartsWith("/admin/", StringComparison.Ordinal)
                || relative.StartsWith("/checkout/sessions/", StringComparison.Ordinal);
            if (!app.Configuration.GetValue<bool>("Entertainments:Enabled") && !continuation)
                return Results.NotFound(ApiResponseHelper.Error("ارائه خدمات جدید فعلاً فعال نیست", statusCode: 404));
            return await next(context);
        });

        foreach (var type in endpointTypes)
        {
            if (Activator.CreateInstance(type) is IEndpoint endpoint)
            {
                endpoint.Map(group);
            }
        }
    }
}

