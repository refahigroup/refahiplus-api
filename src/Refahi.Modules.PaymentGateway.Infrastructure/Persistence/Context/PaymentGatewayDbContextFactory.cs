using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Refahi.Modules.PaymentGateway.Infrastructure.Persistence.Context;

namespace Refahi.Modules.PaymentGateway.Infrastructure.Persistence.Context;

public class PaymentGatewayDbContextFactory : IDesignTimeDbContextFactory<PaymentGatewayDbContext>
{
    public PaymentGatewayDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<PaymentGatewayDbContext>();

        optionsBuilder.UseNpgsql("Host=localhost;Database=refahi-db;Username=postgres;Password=postgres");

        return new PaymentGatewayDbContext(optionsBuilder.Options);
    }
}
