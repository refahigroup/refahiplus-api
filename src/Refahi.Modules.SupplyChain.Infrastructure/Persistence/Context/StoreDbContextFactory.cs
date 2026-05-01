using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Refahi.Modules.SupplyChain.Infrastructure.Persistence.Context;

public class SupplyChainDbContextFactory : IDesignTimeDbContextFactory<SupplyChainDbContext>
{
    public SupplyChainDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<SupplyChainDbContext>();
        optionsBuilder.UseNpgsql(
            "Host=localhost;Database=refahi;Username=refahi;Password=refahi");

        return new SupplyChainDbContext(optionsBuilder.Options);
    }
}

