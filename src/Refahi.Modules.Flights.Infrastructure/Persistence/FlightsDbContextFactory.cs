using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Refahi.Modules.Flights.Infrastructure.Persistence;

public sealed class FlightsDbContextFactory : IDesignTimeDbContextFactory<FlightsDbContext>
{
    public FlightsDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<FlightsDbContext>();
        optionsBuilder.UseNpgsql(
            "Host=localhost;Database=refahi;Username=refahi;Password=refahi",
            options => options.MigrationsHistoryTable("__EFMigrationsHistory", "flights"));

        return new FlightsDbContext(optionsBuilder.Options);
    }
}
