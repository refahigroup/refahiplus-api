using FluentValidation;
using MediatR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Refahi.Modules.Entertainments.Application.Features.Checkout.CancellationParticipant;
using Refahi.Modules.Orders.Application.Contracts.Cancellation;

namespace Refahi.Modules.Entertainments.Application;

public static class DI
{
    public static IServiceCollection RegisterApplication(this IServiceCollection services, IConfiguration configuration
    )
    {
        var assembly = typeof(DI).Assembly;

        services.AddMediatR(assembly)
                .AddValidatorsFromAssembly(assembly);
        services.AddOptions<EntertainmentRuntimeOptions>().Bind(configuration.GetSection(EntertainmentRuntimeOptions.SectionName))
            .Validate(x => x.RecipientRetentionDays is >= 1 and <= 3650 && x.ReconciliationMinutes is >= 1 and <= 1440,
                "Entertainment runtime options are invalid")
            .ValidateOnStart();

        services.AddScoped<EntertainmentIssuanceProcessor>();
        services.AddScoped<Contracts.Providers.IEntertainmentPricingService, Services.EntertainmentPricingService>();
        services.AddScoped<IOrderCancellationParticipant, EntertainmentCancellationParticipant>();

        if (configuration.GetValue<bool>("Entertainments:Enabled") && configuration.GetValue<bool>("Entertainments:CatalogueEnabled") && configuration.GetValue<bool>("Entertainments:Providers:TouristPanel:Enabled"))
        {
            services.AddScoped<Features.Discovery.EntertainmentsCatalogueSource>();
            services.AddSingleton(new Refahi.Modules.Catalogue.Application.Contracts.CatalogueSourceRegistration("entertainments", typeof(Features.Discovery.EntertainmentsCatalogueSource)));
        }
        return services;
    }
}
