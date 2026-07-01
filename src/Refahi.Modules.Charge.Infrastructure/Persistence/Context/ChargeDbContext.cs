using Microsoft.EntityFrameworkCore;

namespace Refahi.Modules.Charge.Infrastructure.Persistence.Context;

public class ChargeDbContext: DbContext
{
    public static readonly string Schema = "charge";

    public ChargeDbContext(DbContextOptions<ChargeDbContext> options) : base(options)
    {
    }


    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.HasDefaultSchema(Schema);

        //modelBuilder.ApplyConfiguration(new Configuration());
    }
}
