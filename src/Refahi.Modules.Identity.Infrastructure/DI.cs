using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Polly;
using Refahi.Modules.Identity.Domain.Repositories;
using Refahi.Modules.Identity.Infrastructure.Persistence.Context;
using Refahi.Modules.Identity.Infrastructure.Repositories;
using Refahi.Shared.Extensions;
using Refahi.Shared.Infrastructure;
using System;
using System.Linq;

namespace Refahi.Modules.Identity.Infrastructure;

public static class DI
{
    public static IServiceCollection RegisterInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        // DbContext
        services.AddDbContext<IdentityDbContext>(options =>
        {
            string connectionString = configuration.GetConnectionString();

            options.UseNpgsql(connectionString);
        });

        // Register repositories
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IUserProfileRepository, UserProfileRepository>();
        services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();

        return services;
    }

    public static void UseInfrastructure(this IServiceProvider provider, bool IsDevelopment)
    {
        //if (!IsDevelopment)
        //    return;

        using var scope = provider.CreateScope();
        var tools = scope.ServiceProvider.GetRequiredService<IDbTools>();

        tools.ApplyMigrations<IdentityDbContext>();

    }
}