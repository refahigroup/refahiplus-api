using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Refahi.Modules.CreditPayments.Application;
using Refahi.Modules.CreditPayments.Infrastructure;

namespace Refahi.Modules.CreditPayments.Api;

public static class DI
{
    public const string ApiPrefix = "/api/credit-payments/v1";

    public static IServiceCollection RegisterCreditPaymentsModule(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services
            .RegisterCreditPaymentsApplication(configuration)
            .RegisterCreditPaymentsInfrastructure(configuration);

        return services;
    }

    public static WebApplication UseCreditPaymentsModule(
        this WebApplication app,
        string endPointsPrefix = ApiPrefix)
    {
        // Phase 01 reserves the prefix but intentionally maps no business endpoints.
        return app;
    }
}
