using Identity.Application.Contracts.Interfaces;
using Identity.Domain.Aggregates;
using Identity.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.Extensions.Http;
using Refahi.Modules.Identity.Domain.Repositories;
using Refahi.Modules.Identity.Infrastructure.Persistence;
using Refahi.Modules.Identity.Infrastructure.Repositories;
using Refahi.Shared.Services.Notification;
using System;
using System.Linq;
using System.Net.Http;

namespace Refahi.Modules.Identity.Infrastructure;

public static class DI
{
    

    public static IServiceCollection RegisterInfrastructure(this IServiceCollection services, IConfiguration configuration, bool isDevelopment = false)
    {
        // Configure EF Core Postgres DbContext for Identity
        var conn = configuration.GetConnectionString("Default") ?? configuration["ConnectionStrings:Default"] ?? "Host=localhost;Database=refahi;Username=refahi;Password=refahi";
        services.AddDbContext<IdentityDbContext>(options =>
            options.UseNpgsql(conn));

        // Register repositories
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IUserProfileRepository, UserProfileRepository>();
        services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();

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

            // Seed data for testing
            //if (!ctx.Users.Any())
            //{
            //    var adminUser = User.Create(mobileNumber: "09123456789", email: "admin@refahi.com");
            //    adminUser.SetPassword("admin123");
            //    adminUser.AssignRole(Roles.Admin);
            //    adminUser.AssignRole(Roles.User);

            //    var normalUser = User.Create(mobileNumber: "09987654321", email: "user@refahi.com");
            //    normalUser.SetPassword("user1234");
            //    normalUser.AssignRole(Roles.User);

            //    ctx.Users.AddRange(adminUser, normalUser);
            //    ctx.SaveChanges();
            //}
        }
    }
}