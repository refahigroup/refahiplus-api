using FluentValidation;
using MediatR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Refahi.Modules.Identity.Application.Features.Auth;
using Refahi.Modules.Identity.Application.Features.Auth.Registration;

namespace Refahi.Modules.Identity.Application;

public static class DI
{
    public static IServiceCollection RegisterApplication(this IServiceCollection services, IConfiguration configuration)
    {
        var assembly = typeof(DI).Assembly;

        services
            .AddMediatR(assembly)
            .AddValidatorsFromAssembly(assembly);

        services
            .AddOptions<IdentityOptions>()
            .Bind(configuration.GetSection(IdentityOptions.SectionName));

        services.AddScoped<IUserRegistrationService, UserRegistrationService>();

        return services;
    }
}
