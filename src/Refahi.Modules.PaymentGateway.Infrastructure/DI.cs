using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Refahi.Modules.PaymentGateway.Application.Contracts.Providers;
using Refahi.Modules.PaymentGateway.Application.Contracts.Repositories;
using Refahi.Modules.PaymentGateway.Infrastructure.Persistence.Context;
using Refahi.Modules.PaymentGateway.Infrastructure.Persistence.Repositories;
using Refahi.Modules.PaymentGateway.Infrastructure.Providers;
using Refahi.Modules.PaymentGateway.Infrastructure.Providers.Jibit;
using Refahi.Modules.PaymentGateway.Infrastructure.Providers.Sep;
using Refahi.Shared.Extensions;
using Refahi.Shared.Infrastructure;
using System;

namespace Refahi.Modules.PaymentGateway.Infrastructure;

public static class DI
{
    public static IServiceCollection RegisterInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        string connectionString = configuration.GetConnectionString();

        services.AddDbContext<PaymentGatewayDbContext>(options =>
        {
            options.UseNpgsql(connectionString);
        });

        services.AddScoped<IPaymentGatewaySessionRepository, PaymentGatewaySessionRepository>();

        // Providers
        services.UseSepProvider(configuration);
        services.AddScoped<IPaymentGatewayProvider>(sp => sp.GetRequiredService<SepPaymentGatewayProvider>());

        services.UseJibitProvider(configuration);
        services.AddScoped<IPaymentGatewayProvider>(sp => sp.GetRequiredService<JibitPaymentGatewayProvider>());

        services.AddScoped<IPaymentGatewayProviderFactory, PaymentGatewayProviderFactory>();

        return services;
    }

    public static void UseInfrastructure(this IServiceProvider provider, bool isDevelopment)
    {
        using var scope = provider.CreateScope();
        var tools = scope.ServiceProvider.GetRequiredService<IDbTools>();
        tools.ApplyMigrations<PaymentGatewayDbContext>();
    }
}
