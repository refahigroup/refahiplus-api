using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Refahi.Modules.CreditPayments.Infrastructure.Persistence;

public sealed class CreditPaymentsDbContextFactory:IDesignTimeDbContextFactory<CreditPaymentsDbContext>
{
    public CreditPaymentsDbContext CreateDbContext(string[] args)
    {
        var connection=Environment.GetEnvironmentVariable("REFAHI_CREDITPAYMENTS_DESIGN_CONNECTION") ?? "Host=localhost;Database=refahi;Username=postgres;Password=postgres";
        return new CreditPaymentsDbContext(new DbContextOptionsBuilder<CreditPaymentsDbContext>().UseNpgsql(connection).Options);
    }
}
