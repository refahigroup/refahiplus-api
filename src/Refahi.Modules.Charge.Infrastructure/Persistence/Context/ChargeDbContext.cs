using Microsoft.EntityFrameworkCore;
using Refahi.Modules.Charge.Domain.Aggregates;

namespace Refahi.Modules.Charge.Infrastructure.Persistence.Context;

public sealed class ChargeDbContext : DbContext
{
    public const string Schema = "charge";

    public ChargeDbContext(DbContextOptions<ChargeDbContext> options) : base(options) 
    { 
    }

    public DbSet<ChargeRequest> ChargeRequests => Set<ChargeRequest>();
    public DbSet<ChargeFulfillmentAttempt> FulfillmentAttempts => Set<ChargeFulfillmentAttempt>();
    public DbSet<ChargePin> ChargePins => Set<ChargePin>();
    public DbSet<ChargeMarkupRule> MarkupRules => Set<ChargeMarkupRule>();


    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ChargeDbContext).Assembly);

        base.OnModelCreating(modelBuilder);
    }
}
