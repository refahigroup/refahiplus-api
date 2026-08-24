using FluentValidation;
using MediatR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Refahi.Modules.Commerce.Application.Features.Checkout.CancellationParticipant;
using Refahi.Modules.Orders.Application.Contracts.Cancellation;

namespace Refahi.Modules.Commerce.Application;

public static class DI
{
    public static IServiceCollection RegisterApplication(this IServiceCollection services, IConfiguration configuration
    )
    {
        var assembly = typeof(DI).Assembly;

        services.AddMediatR(assembly)
                .AddValidatorsFromAssembly(assembly);
        services.AddOptions<CommerceRuntimeOptions>().Bind(configuration.GetSection(CommerceRuntimeOptions.SectionName));

        services.AddScoped<CommerceFulfillmentProcessor>();
        services.AddScoped<IOrderCancellationParticipant, CommerceCancellationParticipant>();

        return services;
    }
}
