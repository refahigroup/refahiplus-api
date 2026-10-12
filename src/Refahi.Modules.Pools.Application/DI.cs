using FluentValidation;
using MediatR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Refahi.Modules.Pools.Application.Contracts;
using Refahi.Modules.Pools.Application.Features.Bookings;
using Refahi.Modules.Orders.Application.Contracts.Payments;
using Refahi.Modules.Orders.Application.Contracts.Cancellation;

namespace Refahi.Modules.Pools.Application;
public static class DI
{
    public static IServiceCollection RegisterApplication(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<PoolsOptions>(configuration.GetSection("Pools"));
        services.AddScoped<PoolBookingService>();
        services.AddScoped<IOrderPaymentParticipant, PoolPaymentParticipant>();
        services.AddScoped<IOrderCancellationParticipant, PoolCancellationParticipant>();
        services.AddMediatR(typeof(DI).Assembly).AddValidatorsFromAssembly(typeof(DI).Assembly);
        return services;
    }
}
