using Refahi.Modules.Cinema.Application.Contracts;
using Refahi.Modules.Cinema.Application.Features.Orders;
using Refahi.Modules.Orders.Application.Contracts.Payments;
using Refahi.Modules.Orders.Application.Contracts.Cancellation;
﻿using FluentValidation;
using MediatR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Refahi.Modules.Cinema.Application;

public static class DI
{
    public static IServiceCollection RegisterApplication(
        this IServiceCollection services,
        IConfiguration configuration
    )
    {
        services.Configure<CinemaOptions>(configuration.GetSection("Cinema"));
        services.AddScoped<CinemaOrderService>();
        services.AddScoped<IOrderPaymentParticipant,CinemaPaymentParticipant>();
        services.AddScoped<IOrderCancellationParticipant,CinemaCancellationParticipant>();
        var assembly = typeof(DI).Assembly;

        services.AddMediatR(assembly)
            .AddValidatorsFromAssembly(assembly);

        return services;
    }
}
