using System.Security.Cryptography.X509Certificates;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Refahi.Modules.CreditPayments.Application.Contracts;
using Refahi.Modules.CreditPayments.Application.Contracts.IntegrationEvents;
using Refahi.Modules.CreditPayments.Domain;
using Refahi.Modules.CreditPayments.Infrastructure.Persistence;

namespace Refahi.Modules.CreditPayments.Infrastructure;

internal static class CreditPaymentsDataProtectionRegistration
{
    public static IServiceCollection AddCreditPaymentsDataProtection(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddOptions<CreditPaymentsDataProtectionOptions>()
            .Bind(configuration.GetSection(CreditPaymentsDataProtectionOptions.SectionName))
            .Validate(Valid, "CreditPayments Data Protection configuration is invalid.")
            .ValidateOnStart();

        var configured = configuration
            .GetSection(CreditPaymentsDataProtectionOptions.SectionName)
            .Get<CreditPaymentsDataProtectionOptions>() ?? new();
        var builder = services.AddDataProtection().SetApplicationName(configured.ApplicationName);
        if (!string.IsNullOrWhiteSpace(configured.KeyRingPath))
            builder.PersistKeysToFileSystem(new DirectoryInfo(configured.KeyRingPath));
        if (!string.IsNullOrWhiteSpace(configured.ProtectionCertificatePath))
        {
            var certificate = X509CertificateLoader.LoadPkcs12FromFile(
                configured.ProtectionCertificatePath,
                configured.ProtectionCertificatePassword);
            builder.ProtectKeysWithCertificate(certificate);
        }

        services.AddHostedService<CreditPaymentsDataProtectionStartupProbe>();
        return services;
    }

    private static bool Valid(CreditPaymentsDataProtectionOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.ApplicationName)) return false;
        if (!options.RequirePersistentKeyRing) return true;
        return string.Equals(
                options.ApplicationName,
                CreditPaymentsDataProtectionOptions.SharedApplicationName,
                StringComparison.Ordinal)
            && !string.IsNullOrWhiteSpace(options.KeyRingPath)
            && Directory.Exists(options.KeyRingPath)
            && !string.IsNullOrWhiteSpace(options.ProtectionCertificatePath)
            && File.Exists(options.ProtectionCertificatePath);
    }
}

internal sealed class CreditPaymentsDataProtectionStartupProbe(
    IOptions<CreditPaymentsDataProtectionOptions> configured,
    IDataProtectionProvider provider) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        if (!configured.Value.RequirePersistentKeyRing) return Task.CompletedTask;
        var protector = provider.CreateProtector("Refahi.CreditPayments.ProductionReadiness.v1");
        var probe = Guid.NewGuid().ToString("N");
        if (!string.Equals(probe, protector.Unprotect(protector.Protect(probe)), StringComparison.Ordinal))
            throw new InvalidOperationException("CreditPayments Data Protection round-trip failed.");
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}

internal sealed class CreditPaymentsConfigurationStartupProbe(
    IOptions<SettlementIntegrationOptions> settlement,
    ICreditPaymentIntegrationEventPublisher publisher) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        if (settlement.Value.PublicationEnabled
            && publisher is UnavailableCreditPaymentIntegrationEventPublisher)
            throw new InvalidOperationException(
                "CreditPayments integration-event publication is enabled without an approved publisher.");
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}

public sealed class CreditPaymentsProductionReadinessChecker(
    CreditPaymentsDbContext db,
    IOptions<MerchantPaymentPolicyOptions> policies,
    IOptions<CreditPaymentsDataProtectionOptions> dataProtection,
    IOptions<CreditPaymentsProductionReadinessOptions> readiness,
    IOptions<SettlementIntegrationOptions> settlement,
    ICreditPaymentIntegrationEventPublisher publisher,
    IDataProtectionProvider protectionProvider,
    TimeProvider clock) : ICreditPaymentsProductionReadinessChecker
{
    public async Task<CreditPaymentsProductionReadinessDto> CheckAsync(CancellationToken ct)
    {
        var rows = new List<ProductionReadinessRequirementDto>();
        var databaseConnected = await db.Database.CanConnectAsync(ct);
        Add(rows, "database", databaseConnected, true,
            databaseConnected ? "CreditPayments PostgreSQL connectivity succeeded." : "CreditPayments PostgreSQL connectivity failed.");

        var dp = dataProtection.Value;
        var dataProtectionReady = dp.RequirePersistentKeyRing
            && string.Equals(dp.ApplicationName,
                CreditPaymentsDataProtectionOptions.SharedApplicationName,
                StringComparison.Ordinal)
            && !string.IsNullOrWhiteSpace(dp.KeyRingPath) && Directory.Exists(dp.KeyRingPath)
            && !string.IsNullOrWhiteSpace(dp.ProtectionCertificatePath) && File.Exists(dp.ProtectionCertificatePath)
            && ProtectionRoundTrip(protectionProvider);
        Add(rows, "data-protection", dataProtectionReady, true,
            dataProtectionReady ? "Persistent shared key ring and certificate protection are configured; round-trip succeeded."
                : "Persistent shared key ring with encryption at rest is not evidenced.");

        if (readiness.Value.FinancialExecutionEnabled)
        {
            var activeTerminals = databaseConnected
                ? await (from terminal in db.Terminals.AsNoTracking()
                    join merchant in db.Merchants.AsNoTracking() on terminal.MerchantId equals merchant.Id
                    where terminal.Status == TerminalStatus.Active && merchant.Status == MerchantStatus.Active
                    select new { merchant.Id, TerminalId = terminal.Id, terminal.ApprovedCategoryCode }).ToListAsync(ct)
                : [];
            var policyCoverage = databaseConnected && activeTerminals.All(x => MatchingPolicies(
                policies.Value.Policies, x.Id, x.TerminalId, x.ApprovedCategoryCode) == 1);
            Add(rows, "merchant-policy-coverage", policyCoverage, true,
                policyCoverage ? $"Exactly one policy resolves for each of {activeTerminals.Count} active terminal/category combinations."
                    : "At least one active terminal/category combination has zero or ambiguous policy matches.");

            var financeApproved = !string.IsNullOrWhiteSpace(policies.Value.FinanceApprovalReference);
            Add(rows, "finance-approval", financeApproved, true,
                financeApproved ? "Finance approval reference is configured." : "Finance approval not evidenced.");
        }
        else
        {
            Add(rows, "financial-execution", true, false, "Financial execution is explicitly disabled for this launch mode.");
        }

        var checkoutReady = !readiness.Value.HostedCheckoutRequired
            || !string.IsNullOrWhiteSpace(readiness.Value.HostedCheckoutDeploymentEvidence);
        Add(rows, "hosted-checkout", checkoutReady, readiness.Value.HostedCheckoutRequired,
            checkoutReady ? "Hosted Checkout deployment evidence is configured or not required."
                : "Hosted Checkout deployment is not evidenced.");

        var transportReady = readiness.Value.SettlementLaunchMode switch
        {
            "gated-no-consumer" => !settlement.Value.PublicationEnabled,
            "required" => settlement.Value.PublicationEnabled
                && publisher is not UnavailableCreditPaymentIntegrationEventPublisher,
            _ => false
        };
        Add(rows, "integration-event-transport", transportReady, true,
            transportReady ? $"Explicit launch mode '{readiness.Value.SettlementLaunchMode}' is coherent with publication configuration."
                : "Approved Settlement publication or explicit gated-no-consumer launch decision is not evidenced.");

        return new(rows.Any(x => x.ProductionBlocker && x.Status == "blocked") ? "blocked" : "ready",
            clock.GetUtcNow(), rows);
    }

    private static int MatchingPolicies(IEnumerable<MerchantPaymentPolicyEntry> entries, Guid merchantId,
        Guid terminalId, string category) => entries.Count(x =>
            (!x.MerchantId.HasValue || x.MerchantId == merchantId)
            && (!x.TerminalId.HasValue || x.TerminalId == terminalId)
            && string.Equals(x.CategoryCode?.Trim(), category, StringComparison.Ordinal));

    private static bool ProtectionRoundTrip(IDataProtectionProvider provider)
    {
        try
        {
            var protector = provider.CreateProtector("Refahi.CreditPayments.ProductionReadiness.v1");
            const string value = "readiness-probe";
            return protector.Unprotect(protector.Protect(value)) == value;
        }
        catch { return false; }
    }

    private static void Add(List<ProductionReadinessRequirementDto> rows, string requirement,
        bool ready, bool blocker, string evidence) => rows.Add(new(requirement, ready ? "ready" : "blocked", blocker, evidence));
}

public sealed class CreditPaymentsDatabaseHealthCheck(CreditPaymentsDbContext db) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken ct = default) =>
        await db.Database.CanConnectAsync(ct)
            ? HealthCheckResult.Healthy("CreditPayments database is reachable.")
            : HealthCheckResult.Unhealthy("پایگاه داده پرداخت اعتباری در دسترس نیست");
}

public sealed class CreditPaymentsReadinessHealthCheck(
    ICreditPaymentsProductionReadinessChecker checker) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken ct = default)
    {
        var result = await checker.CheckAsync(ct);
        return result.Status == "ready"
            ? HealthCheckResult.Healthy("CreditPayments is ready.")
            : HealthCheckResult.Unhealthy("پیش‌نیازهای بهره‌برداری پرداخت اعتباری کامل نیست");
    }
}
