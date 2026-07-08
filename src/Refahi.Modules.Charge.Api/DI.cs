using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Refahi.Modules.Charge.Infrastructure;
using Refahi.Shared.Presentation;
using Refahi.Modules.Charge.Application;
using Microsoft.Extensions.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using System.Threading.RateLimiting;

namespace Refahi.Modules.Charge.Api;

public static class DI
{
    public static IServiceCollection RegisterChargeModule(this IServiceCollection services, IConfiguration configuration)
    {
        var permitLimit = Math.Max(
            1,
            configuration.GetValue("Charge:PublicCatalogRateLimit:PermitLimit", 30));
        var windowSeconds = Math.Max(
            1,
            configuration.GetValue("Charge:PublicCatalogRateLimit:WindowSeconds", 60));

        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.OnRejected = async (context, ct) =>
            {
                context.HttpContext.Response.ContentType = "application/json";
                await context.HttpContext.Response.WriteAsJsonAsync(
                    ApiResponseHelper.Error(
                        "تعداد درخواست‌ها بیش از حد مجاز است. کمی بعد دوباره تلاش کنید",
                        statusCode: StatusCodes.Status429TooManyRequests),
                    ct);
            };
            options.AddPolicy(ChargeRateLimiting.PublicCatalogPolicy, context =>
                RateLimitPartition.GetFixedWindowLimiter(
                    context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = permitLimit,
                        Window = TimeSpan.FromSeconds(windowSeconds),
                        QueueLimit = 0,
                        AutoReplenishment = true
                    }));
        });

        services
            .RegisterApplication(configuration)
            .RegisterInfrastructure(configuration);

        return services;
    }

    public static WebApplication UseChargeModule(this WebApplication app, string endPointsPrefix)
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

    }
}

public static class ChargeRateLimiting
{
    public const string PublicCatalogPolicy = "ChargePublicCatalog";
}
