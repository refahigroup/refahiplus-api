using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Refahi.Modules.Orders.Infrastructure.Persistence.Context;

/// <summary>
/// Design-time factory for creating OrdersDbContext instances for EF Core tools (migrations, etc.)
/// </summary>
public class OrdersDbContextFactory : IDesignTimeDbContextFactory<OrdersDbContext>
{
    public OrdersDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<OrdersDbContext>();

        // Use a default connection string for design-time operations
        // This will be overridden at runtime by DI configuration
        optionsBuilder.UseNpgsql("Host=localhost;Database=refahi;Username=refahi;Password=refahi");

        return new OrdersDbContext(optionsBuilder.Options);
    }
}
