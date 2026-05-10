using FluentValidation;
using MediatR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Refahi.Modules.PaymentGateway.Application;

public static class DI
{
    public static IServiceCollection RegisterApplication(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var assembly = typeof(DI).Assembly;

        services.AddMediatR(assembly);
        services.AddValidatorsFromAssembly(assembly);

        return services;
    }
}
