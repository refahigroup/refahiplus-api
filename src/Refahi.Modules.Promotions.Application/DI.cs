using FluentValidation;
using MediatR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
namespace Refahi.Modules.Promotions.Application;

public static class DI
{
    public static IServiceCollection RegisterApplication(this IServiceCollection services, IConfiguration configuration)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(PromotionExceptionBehavior<,>));
        services.AddMediatR(typeof(DI).Assembly).AddValidatorsFromAssembly(typeof(DI).Assembly);
        return services;
    }
}
