using System.Reflection;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Refahi.Modules.Hotels.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddHotelsApplication(
        this IServiceCollection services,
        IConfiguration configuration
    )
    {
        var assembly = typeof(DependencyInjection).Assembly;

        services.AddMediatR(assembly);
        services.AddValidatorsFromAssembly(assembly);

        return services;
    }
}
