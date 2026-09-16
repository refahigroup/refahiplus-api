using Microsoft.EntityFrameworkCore;

namespace Refahi.Modules.CreditPayments.Infrastructure.Persistence;

public sealed class CreditPaymentsDbContext(DbContextOptions<CreditPaymentsDbContext> options)
    : DbContext(options)
{
    public const string DefaultSchema = "credit_payments";

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(DefaultSchema);
        base.OnModelCreating(modelBuilder);
    }
}
