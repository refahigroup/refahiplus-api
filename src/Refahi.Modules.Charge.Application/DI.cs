using FluentValidation;
using MediatR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Refahi.Modules.Charge.Application.Services;

namespace Refahi.Modules.Charge.Application;

public static class DI
{
    public static IServiceCollection RegisterApplication(this IServiceCollection services, IConfiguration configuration)
    {
        var assembly = typeof(DI).Assembly;

        services
            .AddMediatR(assembly)
            .AddValidatorsFromAssembly(assembly);

        services.AddScoped<ChargePricingService>()
                .AddScoped<ChargeRequestQuoteService>()
                .AddScoped<ChargeFulfillmentProcessor>();

        return services;
    }
}
