using MediatR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Refahi.Modules.CreditPayments.Application;

public static class DI
{
    public static IServiceCollection RegisterCreditPaymentsApplication(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddMediatR(typeof(DI).Assembly);
        return services;
    }
}
