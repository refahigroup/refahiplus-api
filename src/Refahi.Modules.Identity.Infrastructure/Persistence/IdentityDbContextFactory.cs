using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Refahi.Modules.Identity.Infrastructure.Persistence;

/// <summary>
/// Design-time factory for creating IdentityDbContext instances for EF Core tools (migrations, etc.)
/// </summary>
public class IdentityDbContextFactory : IDesignTimeDbContextFactory<IdentityDbContext>
{
    public IdentityDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<IdentityDbContext>();
        
        // Use a default connection string for design-time operations
        // This will be overridden at runtime by DI configuration
        optionsBuilder.UseNpgsql("Host=localhost;Database=refahi;Username=refahi;Password=refahi");

        return new IdentityDbContext(optionsBuilder.Options);
    }
}
