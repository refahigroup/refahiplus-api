using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Refahi.Modules.Store.Api.Security;
using Refahi.Modules.Store.Application;
using Refahi.Modules.Store.Application.Contracts.Vendor;
using Refahi.Modules.Store.Application.Contracts.Vouchers;
using Refahi.Modules.Store.Infrastructure;
using Refahi.Shared.Presentation;

namespace Refahi.Modules.Store.Api;

public static class DI
{
    public static IServiceCollection RegisterStoreModule(
        this IServiceCollection services,
        IConfiguration configuration
    )
    {
        services.AddRateLimiter(options =>
        {
            options.AddPolicy(
                "VendorPos",
                context =>
                    RateLimitPartition.GetFixedWindowLimiter(
                        context.User.FindFirst("sub")?.Value
                            ?? context.Connection.RemoteIpAddress?.ToString()
                            ?? "unknown",
                        _ => new FixedWindowRateLimiterOptions
                        {
                            PermitLimit = 10,
                            Window = TimeSpan.FromMinutes(1),
                            QueueLimit = 0,
                            AutoReplenishment = true,
                        }
                    )
            );
            options.AddPolicy(
                "StoreCart",
                context =>
                    RateLimitPartition.GetFixedWindowLimiter(
                        context.User.FindFirst("sub")?.Value
                            ?? context
                                .User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)
                                ?.Value
                            ?? context.Connection.RemoteIpAddress?.ToString()
                            ?? "unknown",
                        _ => new FixedWindowRateLimiterOptions
                        {
                            PermitLimit = 60,
                            Window = TimeSpan.FromMinutes(1),
                            QueueLimit = 0,
                            AutoReplenishment = true,
                        }
                    )
            );
            options.AddPolicy(
                "VoucherRedeem",
                context =>
                    RateLimitPartition.GetFixedWindowLimiter(
                        context.User.FindFirst("sub")?.Value
                            ?? context
                                .User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)
                                ?.Value
                            ?? context.Connection.RemoteIpAddress?.ToString()
                            ?? "unknown",
                        _ => new FixedWindowRateLimiterOptions
                        {
                            PermitLimit = 20,
                            Window = TimeSpan.FromMinutes(1),
                            QueueLimit = 0,
                            AutoReplenishment = true,
                        }
                    )
            );
        });
        services.AddScoped<InPersonTypedErrorFilter>();
        services.AddScoped<VoucherTypedErrorFilter>();
        services.AddSingleton<IInPersonOtpReferenceProtector, InPersonOtpReferenceProtector>();
        services.AddSingleton<IVoucherCodeProtector, VoucherCodeProtector>();
        services.AddDataProtection();
        services.RegisterApplication(configuration).RegisterInfrastructure(configuration);

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

        group.MapGet("/ping", () => Results.Ok(new { module = "Store Module" }));
    }
}
