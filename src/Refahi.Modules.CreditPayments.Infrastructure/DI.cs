using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Refahi.Modules.CreditPayments.Infrastructure.Persistence;
using Refahi.Shared.Extensions;
using Refahi.Modules.CreditPayments.Application;
using Refahi.Modules.CreditPayments.Application.Contracts;
using Refahi.Modules.CreditPayments.Application.Contracts.IntegrationEvents;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Refahi.Modules.CreditPayments.Domain;

namespace Refahi.Modules.CreditPayments.Infrastructure;

public static class DI
{
    public static IServiceCollection RegisterCreditPaymentsInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddDbContext<CreditPaymentsDbContext>(options =>
            options.UseNpgsql(configuration.GetConnectionString()));
        services.AddCreditPaymentsDataProtection(configuration);
        services.AddScoped<ICreditPaymentsRepository, CreditPaymentsRepository>();
        services.AddScoped<IGatewayPaymentRepository, CreditPaymentsRepository>();
        services.AddScoped<ICheckoutRepository, CreditPaymentsRepository>();
        services.AddScoped<IFundingContinuationRepository, CreditPaymentsRepository>();
        services.AddScoped<IPaymentExecutionRepository, CreditPaymentsRepository>();
        services.AddScoped<IPaymentTerminationRepository, CreditPaymentsRepository>();
        services.AddScoped<IRefundRepository, CreditPaymentsRepository>();
        services.AddScoped<IWebhookOperationsRepository, CreditPaymentsRepository>();
        services.AddScoped<IReconciliationRepository, ReconciliationRepository>();
        services.AddScoped<ICreditPaymentsAdminReader, OperationalAdminReader>();
        services.AddScoped<IReconciliationAdminReader, ReconciliationAdminReader>();
        services.AddScoped<ICreditPaymentsProductionReadinessChecker, CreditPaymentsProductionReadinessChecker>();
        services.AddScoped<CreditPaymentIntegrationEventWorkService>();
        services.AddScoped<SettlementIntegrationEventReconciler>();
        services.AddScoped<ICreditPaymentIntegrationEventReconciler>(sp =>
            sp.GetRequiredService<SettlementIntegrationEventReconciler>());
        services.AddScoped<WebhookWorkService>();
        services.AddScoped<WebhookEventReconciler>();
        services.AddScoped<IWebhookIntegrityRemediator, ExistingWebhookIntegrityRemediator>();
        services.AddScoped<IMerchantPaymentPolicyResolver, ConfigurationMerchantPaymentPolicyResolver>();
        services.AddSingleton<ICredentialSecretService, Pbkdf2CredentialSecretService>();
        services.AddSingleton<IWebhookSecretProtector, DataProtectionWebhookSecretProtector>();
        services.AddSingleton<IWebhookDnsResolver, SystemWebhookDnsResolver>();
        services.AddSingleton<IWebhookEndpointSecurityValidator, WebhookEndpointSecurityValidator>();
        services.AddSingleton<IWebhookHttpTransport, PinnedWebhookHttpTransport>();
        services.AddSingleton<ICheckoutSecurityService, HmacCheckoutSecurityService>();
        services.AddSingleton<ICheckoutOtpReferenceProtector, DataProtectionCheckoutOtpReferenceProtector>();
        services.TryAddSingleton<ICreditPaymentIntegrationEventPublisher,
            UnavailableCreditPaymentIntegrationEventPublisher>();
        services.AddHostedService<CreditPaymentsConfigurationStartupProbe>();
        services.AddHostedService<FundingContinuationRecoveryWorker>();
        services.AddHostedService<AutomaticPaymentResumeWorker>();
        services.AddHostedService<PaymentRecoveryWorker>();
        services.AddHostedService<ExpirationWorker>();
        services.AddHostedService<RefundRecoveryWorker>();
        services.AddHostedService<WebhookEventReconciliationWorker>();
        services.AddHostedService<WebhookOutboxDispatcherWorker>();
        services.AddHostedService<WebhookDeliveryWorker>();
        services.AddHostedService<IntegrityAuditWorker>();
        services.AddHostedService<CreditPaymentIntegrationEventPublisherWorker>();
        services.AddHostedService<SettlementIntegrationEventReconciliationWorker>();
        services.AddHealthChecks()
            .AddCheck<CreditPaymentsDatabaseHealthCheck>("credit-payments-database", tags: ["readiness", "credit-payments"])
            .AddCheck<CreditPaymentsReadinessHealthCheck>("credit-payments-readiness", tags: ["readiness", "credit-payments"]);

        return services;
    }
}
