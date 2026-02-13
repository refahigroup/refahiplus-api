using Identity.Domain.Aggregates;
using Microsoft.EntityFrameworkCore;

namespace Identity.Infrastructure.Persistence.Context;

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
            b.ToTable("Users");

            b.HasKey(u => u.Id);

            b.Property(u => u.Username).IsRequired();
            b.Property(u => u.Username).HasMaxLength(100);

            b.Property(u => u.PasswordHash).IsRequired();
            b.Property(u => u.PasswordHash).HasMaxLength(100);

            b.Property(u => u.Roles).IsRequired();
            b.Property(u => u.Roles).HasMaxLength(200);
        });
    }
}
