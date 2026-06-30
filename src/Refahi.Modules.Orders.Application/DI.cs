using FluentValidation;
using MediatR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Refahi.Modules.Orders.Application.Services;

namespace Refahi.Modules.Orders.Application;

public static class DI
{
    public static IServiceCollection RegisterApplication(this IServiceCollection services, IConfiguration configuration)
    {
        var assembly = typeof(DI).Assembly;

        services
            .AddMediatR(assembly)
            .AddValidatorsFromAssembly(assembly);

        services.AddScoped<IOrderCreationGateway, OrderCreationGateway>();

        //services.AddTransient(typeof(IPipelineBehavior<,>), typeof(BuildingBlocks.ValidationBehavior<,>));


        return services;
    }
}
