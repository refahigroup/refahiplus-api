using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Refahi.Modules.Media.Infrastructure.Persistence.Context;

public class StoreDbContextFactory : IDesignTimeDbContextFactory<MediaDbContext>
{
    public MediaDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<MediaDbContext>();
        optionsBuilder.UseNpgsql(
            "Host=localhost;Database=refahi;Username=refahi;Password=refahi",
            o => o.MigrationsHistoryTable("__EFMigrationsHistory", "store"));

        return new MediaDbContext(optionsBuilder.Options);
    }
}
