using Microsoft.EntityFrameworkCore;
using Refahi.Modules.Identity.Domain.Aggregates;
using Refahi.Modules.Identity.Domain.Entities;
using Refahi.Modules.Identity.Infrastructure.Persistence.Configurations;

namespace Refahi.Modules.Identity.Infrastructure.Persistence.Context;

public class IdentityDbContext : DbContext
{
    public IdentityDbContext(DbContextOptions<IdentityDbContext> options) : base(options)
    {
    }

    public DbSet<User> Users { get; set; } = null!;
    public DbSet<UserProfile> UserProfiles { get; set; } = null!;
    public DbSet<UserRole> UserRoles { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfiguration(new UserConfiguration());
        modelBuilder.ApplyConfiguration(new UserProfileConfiguration());
        modelBuilder.ApplyConfiguration(new UserRoleConfiguration());
    }
}
