using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Refahi.Modules.References.Infrastructure.Persistence.Context;

public class ReferencesDbContextFactory : IDesignTimeDbContextFactory<ReferencesDbContext>
{
    public ReferencesDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<ReferencesDbContext>();
        optionsBuilder.UseNpgsql(
            "Host=localhost;Database=refahi;Username=refahi;Password=refahi",
            o => o.MigrationsHistoryTable("__EFMigrationsHistory", "references"));

        return new ReferencesDbContext(optionsBuilder.Options);
    }
}
