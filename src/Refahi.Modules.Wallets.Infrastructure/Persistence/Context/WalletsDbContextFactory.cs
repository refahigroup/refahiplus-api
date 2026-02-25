using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Refahi.Modules.Wallets.Infrastructure.Persistence.Context;

public class WalletsDbContextFactory : IDesignTimeDbContextFactory<WalletsDbContext>
{
    public WalletsDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<WalletsDbContext>();

        optionsBuilder.UseNpgsql("Host=localhost;Database=refahi-db;Username=refahi;Password=refahi");

        return new WalletsDbContext(optionsBuilder.Options);
    }
}

