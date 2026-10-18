using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Refahi.Modules.Orders.Application;
using Refahi.Modules.Orders.Infrastructure;
using Refahi.Shared.Presentation;

namespace Refahi.Modules.Orders.Api;

public static class DI
{
    public static IServiceCollection RegisterOrdersModule(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment? environment = null
    )
    {
        services.AddSingleton<Refahi.Modules.Orders.Api.Endpoints.PromotionIpLimiter>();
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = 429;
            options.OnRejected = async (context, ct) =>
            {
                context.HttpContext.Response.StatusCode = 429;
                await context.HttpContext.Response.WriteAsJsonAsync(ApiResponseHelper.Error("تعداد تلاش‌ها بیش از حد مجاز است؛ کمی بعد تلاش کنید", statusCode: 429), ct);
            };
            options.AddPolicy("OrderPromotions", context =>
            RateLimitPartition.GetFixedWindowLimiter(
                context.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
                    ?? context.User.FindFirst("sub")?.Value ?? context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                _ => new FixedWindowRateLimiterOptions { PermitLimit = 20, Window = TimeSpan.FromMinutes(1), QueueLimit = 0, AutoReplenishment = true }));
        });
        services.RegisterApplication(configuration).RegisterInfrastructure(configuration);

        return services;
    }

    public static WebApplication UseOrdersModule(this WebApplication app, string endPointsPrefix)
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

        var group = app.MapGroup(endPointsPrefix);

        foreach (var type in endpointTypes)
        {
            if (Activator.CreateInstance(type) is IEndpoint endpoint)
            {
                endpoint.Map(group);
            }
        }

        group.MapGet("/ping", () => Results.Ok(new { module = "Order Module" }));
    }
}
