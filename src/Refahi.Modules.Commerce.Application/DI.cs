using FluentValidation;
using MediatR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Refahi.Modules.Commerce.Application.Contracts.Abstraction;

namespace Refahi.Modules.Commerce.Application;

public static class DI
{
    public static IServiceCollection RegisterApplication(this IServiceCollection services, IConfiguration configuration
    )
    {
        var assembly = typeof(DI).Assembly;

        services.AddMediatR(assembly)
                .AddValidatorsFromAssembly(assembly);

        services.AddSingleton<ICommerceProviderManager, CommerceProviderManager>()
                .AddScoped<ICommerceProvider, CommerceProvider>();

        return services;
    }
}
