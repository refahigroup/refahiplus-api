using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;
using Refahi.Modules.Identity.Api.Services.Auth;
using Refahi.Modules.Identity.Application;
using Refahi.Modules.Identity.Infrastructure;
using Refahi.Shared.Extensions;
using Refahi.Shared.Presentation;
using System.Text;

namespace Refahi.Modules.Identity.Api;

public static class DI
{
    public static IServiceCollection RegisterIdentityModule(this IServiceCollection services, IConfiguration configuration, IHostEnvironment environment)
    {
        services
            .AddOptions<JwtOptions>()
            .Bind(configuration.GetSection(JwtOptions.SectionName))
            .ValidateDataAnnotations()
            .Validate(o => o.IsValid(), "Jwt:Key must be at least 32 chars.")
            .ValidateOnStart();

        services.AddScoped<ITokenService, JwtTokenService>();

   
        services
            .RegisterApplication(configuration)
            .RegisterInfrastructure(configuration, environment.IsDevelopment());

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                .AddJwtBearer();

        services.PostConfigure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, options =>
        {
            var jwt = configuration.GetSection("Jwt");

            var jwtOptions = new JwtOptions
            {
                Key = jwt["Key"]!,
                Issuer = jwt["Issuer"]!,
                Audience = jwt["Audience"]!
            };

            // TODO: if jwtOptions is not Valid ...

            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = jwtOptions.Issuer,
                ValidateAudience = true,
                ValidAudience = jwtOptions.Audience,
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.Key)),
                ValidateLifetime = true,
                ClockSkew = TimeSpan.FromSeconds(int.TryParse(jwt["ClockSkewSeconds"], out var s) ? s : 30)
            };
        });

        services.AddAuthorization(options =>
        {
            // Admin-only policy
            options.AddPolicy("AdminOnly", policy =>
                policy.RequireRole("Admin"));

            // User or Admin policy
            options.AddPolicy("UserOrAdmin", policy =>
                policy.RequireRole("User", "Admin"));

            // Provider or Admin policy
            options.AddPolicy("ProviderOrAdmin", policy =>
                policy.RequireRole("Provider", "Admin"));
        });

        return services;
    }

    public static WebApplication UseIdentityModule(this WebApplication app, string endPointsPrefix)
    {
        MapEndPoints(app, endPointsPrefix);

        app.UseAuthentication();
        app.UseAuthorization();

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
