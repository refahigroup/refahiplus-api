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
        services.AddScoped<Features.Discovery.HotelsCatalogueSource>();
        services.AddSingleton(new Refahi.Modules.Catalogue.Application.Contracts.CatalogueSourceRegistration("hotels",typeof(Features.Discovery.HotelsCatalogueSource)));
        var assembly = typeof(DependencyInjection).Assembly;

        services.AddMediatR(assembly);
        services.AddValidatorsFromAssembly(assembly);

        services.AddScoped<Features.Catalogue.HotelCatalogueService>();
        services.AddScoped<Contracts.Catalogue.IHotelSalePriceCalculator, Features.Catalogue.HotelSalePriceCalculator>();
        services.AddScoped<Refahi.Modules.Orders.Application.Contracts.Payments.IOrderPaymentParticipant, Features.Catalogue.HotelPaymentParticipant>();
        services.AddScoped<Refahi.Modules.Orders.Application.Contracts.Cancellation.IOrderCancellationParticipant, Features.Catalogue.HotelCancellationParticipant>();
        return services;
    }
}
