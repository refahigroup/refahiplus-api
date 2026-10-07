using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
namespace Refahi.Modules.Cinema.Infrastructure.Persistence;
public sealed class CinemaDbContextFactory : IDesignTimeDbContextFactory<CinemaDbContext>
{
    public CinemaDbContext CreateDbContext(string[] args)=>new(new DbContextOptionsBuilder<CinemaDbContext>()
        .UseNpgsql(Environment.GetEnvironmentVariable("REFAHI_DB_CONNECTION")??"Host=localhost;Database=refahi;Username=postgres",
            x=>x.MigrationsHistoryTable("__EFMigrationsHistory",CinemaDbContext.Schema)).Options);
}
