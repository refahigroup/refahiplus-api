using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Refahi.Modules.CreditPayments.Infrastructure.Persistence;
using Refahi.Shared.Extensions;

namespace Refahi.Modules.CreditPayments.Infrastructure;

public static class DI
{
    public static IServiceCollection RegisterCreditPaymentsInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddDbContext<CreditPaymentsDbContext>(options =>
            options.UseNpgsql(configuration.GetConnectionString()));

        return services;
    }
}
