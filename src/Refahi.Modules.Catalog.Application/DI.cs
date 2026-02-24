using FluentValidation;
using MediatR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Refahi.Modules.Catalog.Application;

public static class DI
{
    public static IServiceCollection RegisterApplication(this IServiceCollection services, IConfiguration configuration)
    {
        var assembly = typeof(DI).Assembly;

        services
            .AddMediatR(cfg => cfg.RegisterServicesFromAssembly(assembly))
            .AddValidatorsFromAssembly(assembly);


        return services;
    }
}
