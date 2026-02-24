using FluentValidation;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Refahi.Modules.Wallets.Application.Contracts.Usecases;
using Refahi.Modules.Wallets.Application.Services;

namespace Refahi.Modules.Wallets.Application;

public static class DI
{
    public static IServiceCollection RegisterApplication(this IServiceCollection services, IConfiguration configuration)
    {
        var assembly = typeof(DI).Assembly;

        services
            .AddMediatR(cfg => cfg.RegisterServicesFromAssembly(assembly))
            .AddValidatorsFromAssembly(assembly);

        // Application Services (use case orchestration)
        services.AddScoped<IWalletTopUpUsecase, WalletTopUpApplicationService>();
        services.AddScoped<CreatePaymentIntentApplicationService>();
        services.AddScoped<CapturePaymentIntentApplicationService>();
        services.AddScoped<ReleasePaymentIntentApplicationService>();
        services.AddScoped<RefundPaymentApplicationService>();
        services.AddScoped<BalanceRebuildApplicationService>();

        //services.AddTransient(typeof(IPipelineBehavior<,>), typeof(BuildingBlocks.ValidationBehavior<,>));


        return services;
    }
}