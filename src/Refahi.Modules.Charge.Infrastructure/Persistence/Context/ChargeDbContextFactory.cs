using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
namespace Refahi.Modules.Charge.Infrastructure.Persistence.Context;
public sealed class ChargeDbContextFactory : IDesignTimeDbContextFactory<ChargeDbContext>
{
    public ChargeDbContext CreateDbContext(string[] args)
    {
        var builder = new DbContextOptionsBuilder<ChargeDbContext>();
        builder.UseNpgsql("Host=localhost;Database=refahi;Username=refahi;Password=refahi",
            x => x.MigrationsHistoryTable("__EFMigrationsHistory", ChargeDbContext.Schema));
        return new ChargeDbContext(builder.Options);
    }
}
