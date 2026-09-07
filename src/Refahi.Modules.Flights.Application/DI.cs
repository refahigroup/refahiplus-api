using FluentValidation;
using MediatR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Refahi.Modules.Flights.Application.Services.Airlines;

namespace Refahi.Modules.Flights.Application;

public static class DI
{
    public static IServiceCollection RegisterApplication(
        this IServiceCollection services,
        IConfiguration configuration
    )
    {
        var assembly = typeof(DI).Assembly;

        services.AddMediatR(assembly).AddValidatorsFromAssembly(assembly);
        services.AddScoped<IAirlineLogoResolver, AirlineLogoResolver>();

        return services;
    }
}
