using Identity.Domain.Aggregates;
using Identity.Domain.Repositories;
using Identity.Infrastructure.Persistence.Context;
using Identity.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System;
using System.Linq;

namespace Identity.Infrastructure;

public static class DI
{
    public static IServiceCollection RegisterInfrastructure(this IServiceCollection services, IConfiguration configuration, bool isDevelopment = false)
    {
        // Configure EF Core Postgres DbContext for Identity
        var conn = configuration.GetConnectionString("Default") ?? configuration["ConnectionStrings:Default"] ?? "Host=localhost;Database=refahi;Username=refahi;Password=refahi";
        services.AddDbContext<IdentityDbContext>(options =>
            options.UseNpgsql(conn));

        services.AddScoped<IUserRepository, AuthService>();

        ApplyMigrations(isDevelopment, services.BuildServiceProvider());

        return services;
    }


    private static void ApplyMigrations(bool IsDevelopment, IServiceProvider serviceProvider)
    {
        if (!IsDevelopment)
            return;

        using (var scope = serviceProvider.CreateScope())
        {
            var ctx = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
            ctx.Database.Migrate();

            if (!ctx.Users.Any())
            {
                var adminDomain = UserAggregate.Create("1", "admin", "admin", "admin");
                var userDomain = UserAggregate.Create("2", "user", "user", "user");

                ctx.Users.AddRange(adminDomain, userDomain);
                ctx.SaveChanges();
            }
        }
    }
}