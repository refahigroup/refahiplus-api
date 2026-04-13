using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Refahi.Modules.Orders.Domain.Repositories;
using Refahi.Modules.Orders.Infrastructure.Persistence.Context;
using Refahi.Modules.Orders.Infrastructure.Repositories;
using Refahi.Shared.Extensions;

namespace Refahi.Modules.Orders.Infrastructure;

public static class DI
{
    public static IServiceCollection RegisterInfrastructure(this IServiceCollection services, IConfiguration configuration, bool isDevelopment = false)
    {
        string connectionString = configuration.GetConnectionString();

        services.AddDbContext<OrdersDbContext>(options =>
            options.UseNpgsql(connectionString));

        services.AddScoped<IOrderRepository, OrderRepository>();

        ApplyMigrations(isDevelopment, services.BuildServiceProvider());

        return services;
    }

    private static void ApplyMigrations(bool isDevelopment, IServiceProvider serviceProvider)
    {
        if (!isDevelopment)
            return;

        using var scope = serviceProvider.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<OrdersDbContext>();
        ctx.Database.Migrate();
    }
}
