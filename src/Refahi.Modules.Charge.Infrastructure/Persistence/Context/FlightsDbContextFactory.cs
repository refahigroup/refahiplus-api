using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Refahi.Modules.Charge.Infrastructure.Persistence.Context;

public sealed class FlightsDbContextFactory : IDesignTimeDbContextFactory<ChargeDbContext>
{
    public ChargeDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<ChargeDbContext>();
        optionsBuilder.UseNpgsql(
            "Host=localhost;Database=refahi;Username=refahi;Password=refahi",
            options => options.MigrationsHistoryTable("__EFMigrationsHistory", ChargeDbContext.Schema));

        return new ChargeDbContext(optionsBuilder.Options);
    }
}
