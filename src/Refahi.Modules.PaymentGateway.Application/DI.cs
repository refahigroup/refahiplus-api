using FluentValidation;
using MediatR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Refahi.Modules.PaymentGateway.Application.Contracts.Configuration;
using Refahi.Modules.PaymentGateway.Application.Contracts.Funding;
using Refahi.Modules.PaymentGateway.Application.Funding;

namespace Refahi.Modules.PaymentGateway.Application;

public static class DI
{
    public static IServiceCollection RegisterApplication(
        this IServiceCollection services,
        IConfiguration configuration
    )
    {
        var assembly = typeof(DI).Assembly;

        services.Configure<FundingSafetyOptions>(configuration.GetSection("PaymentGateway"));

        services.AddMediatR(assembly);
        services.AddValidatorsFromAssembly(assembly);
        services.AddScoped<IWalletFundingService, WalletFundingService>();

        return services;
    }
}
