using MediatR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Refahi.Modules.CreditPayments.Application.Contracts;
using Refahi.Modules.CreditPayments.Application.Contracts.IntegrationEvents;

namespace Refahi.Modules.CreditPayments.Application;

public static class DI
{
    public static IServiceCollection RegisterCreditPaymentsApplication(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddMediatR(typeof(DI).Assembly);
        services.AddValidatorsFromAssembly(typeof(DI).Assembly);
        services.AddScoped<IMerchantCredentialAuthenticator, MerchantCredentialAuthenticator>();
        services.AddScoped<FundingContinuationReconciler>();
        services.AddOptions<GatewayPaymentOptions>().Bind(configuration.GetSection(GatewayPaymentOptions.SectionName))
            .Validate(x=>x.MinimumExpirySeconds>0&&x.DefaultExpirySeconds>=x.MinimumExpirySeconds&&x.MaximumExpirySeconds>=x.DefaultExpirySeconds,"Gateway payment expiry bounds are invalid.").ValidateOnStart();
        services.AddOptions<CheckoutOptions>().Bind(configuration.GetSection(CheckoutOptions.SectionName))
            .Validate(x=>Uri.TryCreate(x.PublicBaseUrl,UriKind.Absolute,out var u)&&u.Scheme=="https"&&x.ActiveTokenKeyVersion>0&&x.TokenKeys.ContainsKey(x.ActiveTokenKeyVersion)&&x.OtpResendCooldownSeconds>0&&x.MaximumOtpSends>0&&x.MaximumOtpVerifyAttempts>0&&x.SyntheticChallengeExpirySeconds>0,"Checkout security configuration is invalid.").ValidateOnStart();
        services.AddOptions<FundingContinuationOptions>().Bind(configuration.GetSection(FundingContinuationOptions.SectionName))
            .Validate(x=>ValidWorker(x.RecoveryPollSeconds,x.RecoveryBatchSize,x.ProcessingLeaseSeconds,x.BaseRetrySeconds,x.MaximumRetrySeconds)&&x.MaximumAttemptsPerPayment>0,"Funding continuation worker configuration is invalid.").ValidateOnStart();
        services.AddOptions<PaymentExecutionOptions>().Bind(configuration.GetSection(PaymentExecutionOptions.SectionName))
            .Validate(x=>ValidWorker(x.RecoveryPollSeconds,x.RecoveryBatchSize,x.ProcessingLeaseSeconds,x.BaseRetrySeconds,x.MaximumRetrySeconds)&&x.AutoResumePollSeconds>0,"Payment execution worker configuration is invalid.").ValidateOnStart();
        services.AddOptions<MerchantPaymentPolicyOptions>().Bind(configuration.GetSection(MerchantPaymentPolicyOptions.SectionName))
            .Validate(ValidMerchantPolicies,"Merchant payment policy configuration is invalid or ambiguous.").ValidateOnStart();
        services.AddOptions<PaymentTerminationOptions>().Bind(configuration.GetSection(PaymentTerminationOptions.SectionName))
            .Validate(x=>x.ExpirationPollSeconds>0&&x.ExpirationBatchSize is >0 and <=500,"Payment termination worker configuration is invalid.").ValidateOnStart();
        services.AddOptions<RefundOptions>().Bind(configuration.GetSection(RefundOptions.SectionName))
            .Validate(x=>x.RecoveryPollSeconds>0&&x.LeaseSeconds>0&&x.BatchSize is >0 and <=500&&x.MaximumBackoffSeconds>0,"Refund worker configuration is invalid.").ValidateOnStart();
        services.AddOptions<WebhookOptions>().Bind(configuration.GetSection(WebhookOptions.SectionName))
            .Validate(x=>x.OutboxPollSeconds>0&&x.DeliveryPollSeconds>0&&x.ReconciliationPollSeconds>0&&x.BatchSize is >0 and <=500&&x.LeaseSeconds>0&&x.TimeoutSeconds is >0 and <=120&&x.MaximumAttempts>0&&x.BaseRetrySeconds>0&&x.MaximumRetrySeconds>=x.BaseRetrySeconds&&x.AllowedPorts.Length>0&&x.AllowedPorts.All(p=>p is >0 and <=65535),"Webhook configuration is invalid.").ValidateOnStart();
        services.AddOptions<RiskOptions>().Bind(configuration.GetSection(RiskOptions.SectionName))
            .Validate(ValidRisk,"Risk configuration is invalid; enabled rules require positive limits and UTC buckets.").ValidateOnStart();
        services.AddOptions<ReconciliationOptions>().Bind(configuration.GetSection(ReconciliationOptions.SectionName))
            .Validate(ValidReconciliation,"Reconciliation configuration is invalid.").ValidateOnStart();
        services.AddOptions<SettlementIntegrationOptions>().Bind(configuration.GetSection(SettlementIntegrationOptions.SectionName))
            .Validate(x=>x.PublicationPollSeconds>0&&x.ReconciliationPollSeconds>0&&x.BatchSize is >0 and <=500&&
                x.LeaseSeconds>0&&x.BaseRetrySeconds>0&&x.MaximumRetrySeconds>=x.BaseRetrySeconds,
                "Settlement integration-event configuration is invalid.").ValidateOnStart();
        services.AddOptions<CreditPaymentsProductionReadinessOptions>()
            .Bind(configuration.GetSection(CreditPaymentsProductionReadinessOptions.SectionName))
            .Validate(x=>string.IsNullOrWhiteSpace(x.SettlementLaunchMode)||
                x.SettlementLaunchMode is "required" or "gated-no-consumer",
                "Production readiness settlement launch mode is invalid.")
            .ValidateOnStart();
        services.TryAddSingleton(TimeProvider.System);
        return services;
    }

    private static bool ValidWorker(int poll,int batch,int lease,int minimumBackoff,int maximumBackoff) =>
        poll>0&&batch is >0 and <=500&&lease>0&&minimumBackoff>0&&maximumBackoff>=minimumBackoff;

    private static bool ValidMerchantPolicies(MerchantPaymentPolicyOptions x)
    {
        if (x.Policies.Any(p=>p.DestinationWalletId==Guid.Empty||p.EffectiveAt==default||
            string.IsNullOrWhiteSpace(p.PolicyId)||p.PolicyId.Trim().Length>100||
            string.IsNullOrWhiteSpace(p.Version)||p.Version.Trim().Length>100||
            string.IsNullOrWhiteSpace(p.CategoryCode)||p.CategoryCode.Trim().Length>100)) return false;
        return x.Policies
            .GroupBy(p=>new { p.MerchantId,p.TerminalId,Category=p.CategoryCode.Trim().ToLowerInvariant() })
            .All(g=>g.Count()==1);
    }

    private static bool ValidRisk(RiskOptions x) =>
        string.Equals(x.BucketTimeZone,"UTC",StringComparison.Ordinal)&&
        x.TerminalOverrides.Select(o=>o.TerminalId).Distinct().Count()==x.TerminalOverrides.Count&&
        x.TerminalOverrides.All(o=>o.TerminalId!=Guid.Empty&&(o.Payments is null||ValidLimit(o.Payments))&&(o.Refunds is null||ValidLimit(o.Refunds)))&&
        ValidLimit(x.TerminalPayments)&&ValidLimit(x.CustomerPayments)&&ValidLimit(x.TerminalRefunds)&&ValidLimit(x.CustomerFundingStarts);

    private static bool ValidLimit(RiskLimitOptions x) => !x.Enabled||
        ((x.DailyCountLimit is >0||x.DailyAmountLimitMinor is >0)&&
         (x.DailyCountLimit is null or >0)&&(x.DailyAmountLimitMinor is null or >0));

    private static bool ValidReconciliation(ReconciliationOptions x) => !x.Enabled ||
        (x.PollIntervalSeconds>0&&x.BatchSize is >0 and <=500&&x.LeaseSeconds>0&&x.LookbackHours>0&&
         x.MaximumHistoricalRangeDays is >0 and <=366&&x.ProcessingStaleAfterMinutes>=0&&
         x.FundingStaleAfterMinutes>=0&&x.RefundStaleAfterMinutes>=0&&x.ProjectionStaleAfterMinutes>=0&&
         x.DeadLetterStaleAfterMinutes>=0&&x.MaxAutoRemediationAttempts>0&&x.BaseRetrySeconds>0&&
         x.MaximumRetrySeconds>=x.BaseRetrySeconds);
}
