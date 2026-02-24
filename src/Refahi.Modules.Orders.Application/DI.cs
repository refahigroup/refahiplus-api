using FluentValidation;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Refahi.Modules.Orders.Application;

public static class DI
{
    public static IServiceCollection RegisterApplication(this IServiceCollection services, IConfiguration configuration)
    {
        var assembly = typeof(DI).Assembly;

        services
            .AddMediatR(cfg => cfg.RegisterServicesFromAssembly(assembly))
            .AddValidatorsFromAssembly(assembly);

        //services.AddTransient(typeof(IPipelineBehavior<,>), typeof(BuildingBlocks.ValidationBehavior<,>));


        return services;
    }
}