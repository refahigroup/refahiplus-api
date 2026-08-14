using FluentValidation;
using MediatR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Refahi.Modules.Store.Application.Services;

namespace Refahi.Modules.Store.Application;

public static class DI
{
    public static IServiceCollection RegisterApplication(
        this IServiceCollection services,
        IConfiguration configuration
    )
    {
        var assembly = typeof(DI).Assembly;

        services.TryAddSingleton(TimeProvider.System);

        services
            .AddMediatR(assembly)
            .AddValidatorsFromAssembly(assembly)
            .AddScoped<IModuleResolver, ModuleResolver>()
            .AddSingleton<IStoreBusinessClock, StoreBusinessClock>()
            .AddScoped<IDeliveryService, DeliveryService>()
            .AddScoped<IStoreInPersonFinancialPlanner, StoreInPersonFinancialPlanner>();
        services.AddScoped<IOnlineOfferEligibilityService, OnlineOfferEligibilityService>();

        services
            .AddOptions<StorePaymentDistributionOptions>()
            .Bind(configuration.GetSection(StorePaymentDistributionOptions.SectionName))
            .Validate(
                x => x.RefahiRevenueWalletId != Guid.Empty,
                "شناسه کیف درآمد رفاهی الزامی است"
            )
            .Validate(x => x.RefahiVatWalletId != Guid.Empty, "شناسه کیف مالیات رفاهی الزامی است")
            .Validate(
                x => x.RefahiRevenueWalletId != x.RefahiVatWalletId,
                "کیف درآمد و مالیات باید متفاوت باشند"
            )
            .Validate(x => x.VatRatePercent is >= 0 and <= 100, "نرخ مالیات نامعتبر است")
            .ValidateOnStart();

        return services;
    }
}
