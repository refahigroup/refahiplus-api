using Identity.Domain.Aggregates;
using Microsoft.EntityFrameworkCore;

namespace Identity.Infrastructure;

public class IdentityDbContext : DbContext
{
    public IdentityDbContext(DbContextOptions<IdentityDbContext> options) : base(options)
    {
    }

    public DbSet<UserAggregate> Users { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<UserAggregate>(b =>
        {
            b.HasKey(u => u.Id);
            b.Property(u => u.Username).IsRequired();
            b.Property(u => u.PasswordHash).IsRequired();
            b.Property(u => u.Roles).IsRequired();
        });
    }
}
