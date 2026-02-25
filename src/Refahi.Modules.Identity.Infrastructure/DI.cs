using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Refahi.Modules.Identity.Domain.Repositories;
using Refahi.Modules.Identity.Infrastructure.Persistence.Context;
using Refahi.Modules.Identity.Infrastructure.Repositories;
using System;
using Refahi.Shared.Extensions;

namespace Refahi.Modules.Identity.Infrastructure;

public static class DI
{
    

    public static IServiceCollection RegisterInfrastructure(this IServiceCollection services, IConfiguration configuration, bool isDevelopment = false)
    {
        string connectionString = configuration.GetConnectionString();

        services.AddDbContext<IdentityDbContext>(options =>
            options.UseNpgsql(connectionString));

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