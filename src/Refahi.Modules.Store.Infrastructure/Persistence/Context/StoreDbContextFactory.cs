using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Refahi.Modules.Store.Infrastructure.Persistence.Context;

public class StoreDbContextFactory : IDesignTimeDbContextFactory<StoreDbContext>
{
    public StoreDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<StoreDbContext>();
        optionsBuilder.UseNpgsql(
            "Host=localhost;Database=refahi;Username=refahi;Password=refahi",
            o => o.MigrationsHistoryTable("__EFMigrationsHistory", "store"));

        return new StoreDbContext(optionsBuilder.Options);
    }
}
