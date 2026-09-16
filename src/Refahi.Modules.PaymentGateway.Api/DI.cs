using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Refahi.Modules.PaymentGateway.Application;
using Refahi.Modules.PaymentGateway.Infrastructure;
using Refahi.Shared.Presentation;
using System.Threading.RateLimiting;

namespace Refahi.Modules.PaymentGateway.Api;

public static class DI
{
    public static IServiceCollection RegisterPaymentGatewayModule(
        this IServiceCollection services,
        IConfiguration configuration
    )
    {
        services.RegisterApplication(configuration);
        services.RegisterInfrastructure(configuration);
        var permitLimit = configuration.GetValue("PaymentGateway:CallbackPermitLimit", 60);
        var windowSeconds = configuration.GetValue("PaymentGateway:CallbackWindowSeconds", 60);
        services.AddRateLimiter(options => options.AddPolicy("FundingCallbacks", context =>
            RateLimitPartition.GetFixedWindowLimiter(
                context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = Math.Max(1, permitLimit),
                    Window = TimeSpan.FromSeconds(Math.Max(1, windowSeconds)),
                    QueueLimit = 0,
                })));

        return services;
    }

    public static WebApplication UsePaymentGatewayModule(
        this WebApplication app,
        string endPointsPrefix
    )
    {
        app.Services.UseInfrastructure(app.Environment.IsDevelopment());

        MapEndPoints(app, endPointsPrefix);

        return app;
    }

    private static void MapEndPoints(WebApplication app, string endPointsPrefix)
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

        group.MapGet("/ping", () => Results.Ok(ApiResponseHelper.Success(
                new { module = "PaymentGateway Module" })))
            .WithName("PaymentGateway.Ping")
            .WithTags("PaymentGateway");
    }
}
