using FluentValidation;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Refahi.Modules.Store.Application.Services;

namespace Refahi.Modules.Store.Application;

public static class DI
{
    public static IServiceCollection RegisterApplication(this IServiceCollection services, IConfiguration configuration)
    {
        var assembly = typeof(DI).Assembly;

        services
            .AddMediatR(cfg => cfg.RegisterServicesFromAssembly(assembly))
            .AddValidatorsFromAssembly(assembly)
            .AddScoped<IModuleResolver, ModuleResolver>();

        return services;
    }
}