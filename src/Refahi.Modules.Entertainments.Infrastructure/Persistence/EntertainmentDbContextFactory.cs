using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Refahi.Modules.Entertainments.Infrastructure.Persistence;

public sealed class EntertainmentDbContextFactory : IDesignTimeDbContextFactory<EntertainmentDbContext>
{
    public EntertainmentDbContext CreateDbContext(string[] args)
    {
        var connection = Environment.GetEnvironmentVariable("REFAHI_DB_CONNECTION")
            ?? "Host=localhost;Port=5432;Database=refahi;Username=postgres;Password=postgres";
        return new EntertainmentDbContext(new DbContextOptionsBuilder<EntertainmentDbContext>().UseNpgsql(connection,
            x => x.MigrationsHistoryTable("__EFMigrationsHistory", EntertainmentDbContext.Schema)).Options);
    }
}
