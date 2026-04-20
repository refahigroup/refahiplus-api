using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Refahi.Modules.Orders.Application.Contracts.Repositories;
using Refahi.Modules.Orders.Domain.Repositories;
using Refahi.Modules.Orders.Infrastructure.Outbox;
using Refahi.Modules.Orders.Infrastructure.Persistence.Context;
using Refahi.Modules.Orders.Infrastructure.Repositories;
using Refahi.Shared.Extensions;
using Refahi.Shared.Infrastructure;

namespace Refahi.Modules.Orders.Infrastructure;

public static class DI
{
    public static IServiceCollection RegisterInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        string connectionString = configuration.GetConnectionString();

        services.AddDbContext<OrdersDbContext>(options =>
            options.UseNpgsql(connectionString));

        services.AddScoped<IOrderRepository, OrderRepository>();
        services.AddScoped<IOrderQueryService, OrderQueryService>();
        services.AddHostedService<ProcessOutboxMessagesJob>();

        return services;
    }

    public static void UseInfrastructure(this IServiceProvider provider, bool IsDevelopment)
    {
        //if (!IsDevelopment)
        //    return;

        using var scope = provider.CreateScope();
        var tools = scope.ServiceProvider.GetRequiredService<IDbTools>();

        tools.ApplyMigrations<OrdersDbContext>();

    }
}
