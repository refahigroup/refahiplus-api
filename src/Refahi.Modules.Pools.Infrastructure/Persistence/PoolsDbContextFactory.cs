using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Refahi.Modules.Pools.Infrastructure.Persistence;
public sealed class PoolsDbContextFactory : IDesignTimeDbContextFactory<PoolsDbContext>
{
    public PoolsDbContext CreateDbContext(string[] args) => new(new DbContextOptionsBuilder<PoolsDbContext>()
        .UseNpgsql("", p => p.MigrationsHistoryTable("__EFMigrationsHistory", PoolsDbContext.Schema)).Options);
}
