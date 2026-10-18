using FluentValidation;
using MediatR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
namespace Refahi.Modules.Organizations.Application;

public static class DI
{
    public static IServiceCollection RegisterApplication(this IServiceCollection services, IConfiguration configuration)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.AddMediatR(typeof(DI).Assembly).AddValidatorsFromAssembly(typeof(DI).Assembly);
        return services;
    }
}
