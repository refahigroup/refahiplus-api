using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Refahi.Modules.Commerce.Infrastructure.Persistence;

public sealed class CommerceDbContextFactory : IDesignTimeDbContextFactory<CommerceDbContext>
{
    public CommerceDbContext CreateDbContext(string[] args)
    {
        var connection = Environment.GetEnvironmentVariable("REFAHI_DB_CONNECTION")
            ?? "Host=localhost;Port=5432;Database=refahi;Username=postgres;Password=postgres";
        return new CommerceDbContext(new DbContextOptionsBuilder<CommerceDbContext>().UseNpgsql(connection,
            x => x.MigrationsHistoryTable("__EFMigrationsHistory", CommerceDbContext.Schema)).Options);
    }
}
