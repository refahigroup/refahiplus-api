using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Refahi.Modules.CreditPayments.Application;
using Refahi.Modules.CreditPayments.Application.Contracts;
using Refahi.Modules.CreditPayments.Infrastructure;

namespace Refahi.Modules.CreditPayments.Tests;

public sealed class Phase14ProductionHardeningTests
{
    [Fact]
    public async Task Actual_registration_supports_bidirectional_restart_and_restore_interoperability()
    {
        var root = CreateTemporaryDirectory();
        var keyRingPath = Path.Combine(root, "key-ring");
        var backupPath = Path.Combine(root, "key-ring-backup");
        var certificatePath = Path.Combine(root, "protection.pfx");
        const string certificatePassword = "test-only-password";
        Directory.CreateDirectory(keyRingPath);
        try
        {
            WriteTestCertificate(certificatePath, certificatePassword);
            var values = PersistentDataProtectionConfiguration(
                keyRingPath, certificatePath, certificatePassword);
            const string payloadA = "representative-webhook-secret-from-a";
            const string payloadB = "representative-webhook-secret-from-b";
            string protectedByA;
            string protectedByB;

            using (var instanceA = BuildDataProtectionHost(values))
            using (var instanceB = BuildDataProtectionHost(values))
            {
                await instanceA.StartAsync();
                await instanceB.StartAsync();
                var protectorA = instanceA.Services.GetRequiredService<IDataProtectionProvider>()
                    .CreateProtector("Refahi.CreditPayments.WebhookSigningSecret.v1");
                var protectorB = instanceB.Services.GetRequiredService<IDataProtectionProvider>()
                    .CreateProtector("Refahi.CreditPayments.WebhookSigningSecret.v1");

                protectedByA = protectorA.Protect(payloadA);
                Assert.Equal(payloadA, protectorB.Unprotect(protectedByA));
                protectedByB = protectorB.Protect(payloadB);
                Assert.Equal(payloadB, protectorA.Unprotect(protectedByB));
            }

            Directory.CreateDirectory(backupPath);
            foreach (var file in Directory.GetFiles(keyRingPath))
                File.Copy(file, Path.Combine(backupPath, Path.GetFileName(file)));

            using (var restartedInstance = BuildDataProtectionHost(values))
            {
                await restartedInstance.StartAsync();
                var protector = restartedInstance.Services.GetRequiredService<IDataProtectionProvider>()
                    .CreateProtector("Refahi.CreditPayments.WebhookSigningSecret.v1");
                Assert.Equal(payloadA, protector.Unprotect(protectedByA));
                Assert.Equal(payloadB, protector.Unprotect(protectedByB));
            }

            foreach (var file in Directory.GetFiles(keyRingPath)) File.Delete(file);
            foreach (var file in Directory.GetFiles(backupPath))
                File.Copy(file, Path.Combine(keyRingPath, Path.GetFileName(file)));

            using (var restoredInstance = BuildDataProtectionHost(values))
            {
                await restoredInstance.StartAsync();
                var protector = restoredInstance.Services.GetRequiredService<IDataProtectionProvider>()
                    .CreateProtector("Refahi.CreditPayments.WebhookSigningSecret.v1");
                Assert.Equal(payloadA, protector.Unprotect(protectedByA));
            }

            var keyDocument = string.Join(string.Empty,
                Directory.GetFiles(keyRingPath, "*.xml").Select(File.ReadAllText));
            Assert.Contains("EncryptedSecret", keyDocument, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(payloadA, keyDocument, StringComparison.Ordinal);
            Assert.DoesNotContain(payloadB, keyDocument, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public async Task Persistent_registration_fails_closed_when_key_ring_configuration_is_missing()
    {
        var values = PersistentDataProtectionConfiguration(string.Empty, string.Empty, string.Empty);
        using var host = BuildDataProtectionHost(values);

        await Assert.ThrowsAsync<OptionsValidationException>(() => host.StartAsync());
    }

    [Fact]
    public async Task Persistent_registration_fails_closed_when_key_ring_directory_is_unavailable()
    {
        var root = CreateTemporaryDirectory();
        try
        {
            var unavailablePath = Path.Combine(root, "does-not-exist");
            var certificatePath = Path.Combine(root, "protection.pfx");
            WriteTestCertificate(certificatePath, "test-only-password");
            var values = PersistentDataProtectionConfiguration(
                unavailablePath, certificatePath, "test-only-password");
            using var host = BuildDataProtectionHost(values);

            await Assert.ThrowsAsync<OptionsValidationException>(() => host.StartAsync());
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public async Task Persistent_registration_fails_closed_when_certificate_is_missing()
    {
        var root = CreateTemporaryDirectory();
        try
        {
            var values = PersistentDataProtectionConfiguration(root, string.Empty, string.Empty);
            using var host = BuildDataProtectionHost(values);

            await Assert.ThrowsAsync<OptionsValidationException>(() => host.StartAsync());
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public void Persistent_registration_fails_closed_for_invalid_certificate_password()
    {
        var root = CreateTemporaryDirectory();
        var certificatePath = Path.Combine(root, "protection.pfx");
        try
        {
            WriteTestCertificate(certificatePath, "correct-test-only-password");
            var values = PersistentDataProtectionConfiguration(
                root, certificatePath, "incorrect-test-only-password");

            Assert.ThrowsAny<CryptographicException>(() => BuildDataProtectionHost(values));
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public async Task Persistent_registration_rejects_mismatched_application_name()
    {
        var root = CreateTemporaryDirectory();
        var certificatePath = Path.Combine(root, "protection.pfx");
        try
        {
            WriteTestCertificate(certificatePath, "test-only-password");
            var values = PersistentDataProtectionConfiguration(
                root, certificatePath, "test-only-password");
            values["CreditPayments:DataProtection:ApplicationName"] = "Refahi.CreditPayments.Wrong";
            using var host = BuildDataProtectionHost(values);

            await Assert.ThrowsAsync<OptionsValidationException>(() => host.StartAsync());
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public void Ambiguous_policy_configuration_fails_options_validation()
    {
        var merchantId = Guid.NewGuid();
        var values = ValidConfiguration();
        values["CreditPayments:MerchantPaymentPolicy:Policies:0:MerchantId"] = merchantId.ToString();
        values["CreditPayments:MerchantPaymentPolicy:Policies:1:MerchantId"] = merchantId.ToString();
        values["CreditPayments:MerchantPaymentPolicy:Policies:1:TerminalId"] = values["CreditPayments:MerchantPaymentPolicy:Policies:0:TerminalId"];
        values["CreditPayments:MerchantPaymentPolicy:Policies:1:CategoryCode"] = "store";
        values["CreditPayments:MerchantPaymentPolicy:Policies:1:PolicyId"] = "duplicate";
        values["CreditPayments:MerchantPaymentPolicy:Policies:1:Version"] = "1";
        values["CreditPayments:MerchantPaymentPolicy:Policies:1:DestinationWalletId"] = Guid.NewGuid().ToString();
        values["CreditPayments:MerchantPaymentPolicy:Policies:1:EffectiveAt"] = DateTimeOffset.UtcNow.ToString("O");

        using var provider = BuildApplicationProvider(values);
        Assert.Throws<OptionsValidationException>(() =>
            provider.GetRequiredService<IOptions<MerchantPaymentPolicyOptions>>().Value);
    }

    [Fact]
    public void Readiness_contract_and_endpoint_do_not_expose_sensitive_configuration()
    {
        var forbidden = new[] { "DestinationWalletId", "TokenKey", "Secret", "Password", "KeyRingPath", "CertificatePath" };
        var properties = typeof(ProductionReadinessRequirementDto).GetProperties()
            .Concat(typeof(CreditPaymentsProductionReadinessDto).GetProperties())
            .Select(x => x.Name).ToArray();
        Assert.DoesNotContain(properties, name => forbidden.Any(x => name.Contains(x, StringComparison.OrdinalIgnoreCase)));

        var source = File.ReadAllText(Path.Combine(FindRepositoryRoot(), "src",
            "Refahi.Modules.CreditPayments.Api", "OperationalAdminEndpoints.cs"));
        Assert.Contains("/admin/production-readiness", source, StringComparison.Ordinal);
        Assert.Contains("CreditPayments.Admin.ProductionReadiness", source, StringComparison.Ordinal);
        Assert.Contains("RequireAuthorization(\"AdminOnly\")", source, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Readiness_health_is_unhealthy_when_data_protection_gate_is_blocked()
    {
        var checker = new StubReadinessChecker(new CreditPaymentsProductionReadinessDto(
            "blocked",
            DateTimeOffset.UtcNow,
            [new("data-protection", "blocked", true,
                "Persistent shared key ring with encryption at rest is not evidenced.")]));
        var healthCheck = new CreditPaymentsReadinessHealthCheck(checker);

        var result = await healthCheck.CheckHealthAsync(new HealthCheckContext());

        Assert.Equal(HealthStatus.Unhealthy, result.Status);
        Assert.DoesNotContain("path", result.Description!, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("password", result.Description!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Live_health_route_remains_process_liveness_only()
    {
        var source = File.ReadAllText(Path.Combine(FindRepositoryRoot(), "src", "Refahi.Api", "Program.cs"));

        Assert.Contains("app.MapGet(\"/api/health/live\", () => Results.Ok", source, StringComparison.Ordinal);
        Assert.Contains("\"/api/health/ready\"", source, StringComparison.Ordinal);
        Assert.Contains("registration.Tags.Contains(\"readiness\")", source, StringComparison.Ordinal);
    }

    [Fact]
    public void Production_configuration_requires_persistent_encrypted_data_protection()
    {
        var json = File.ReadAllText(Path.Combine(FindRepositoryRoot(), "src", "Refahi.Api", "appsettings.json"));
        Assert.Contains("\"RequirePersistentKeyRing\": true", json, StringComparison.Ordinal);
        Assert.Contains("\"ApplicationName\": \"Refahi.CreditPayments\"", json, StringComparison.Ordinal);
        Assert.Contains("\"ProtectionCertificatePath\"", json, StringComparison.Ordinal);
    }

    [Fact]
    public void Hosted_checkout_transport_contract_is_locked_down_in_source()
    {
        var root = FindRepositoryRoot();
        var endpoints = File.ReadAllText(Path.Combine(root, "src",
            "Refahi.Modules.CreditPayments.Api", "PaymentAndCheckoutEndpoints.cs"));
        var program = File.ReadAllText(Path.Combine(root, "src", "Refahi.Api", "Program.cs"));
        var settings = File.ReadAllText(Path.Combine(root, "src", "Refahi.Api", "appsettings.json"));

        Assert.Contains("__Host-refahi-cpg-checkout", endpoints, StringComparison.Ordinal);
        Assert.Contains("HttpOnly = true", endpoints, StringComparison.Ordinal);
        Assert.Contains("Secure = true", endpoints, StringComparison.Ordinal);
        Assert.Contains("SameSite = SameSiteMode.Lax", endpoints, StringComparison.Ordinal);
        Assert.Contains("Path = \"/\"", endpoints, StringComparison.Ordinal);
        Assert.DoesNotContain("Domain =", endpoints, StringComparison.Ordinal);
        Assert.Contains("AllowCredentials()", program, StringComparison.Ordinal);
        Assert.Contains("no-store, no-cache", program, StringComparison.Ordinal);
        Assert.Contains("https://pay.refahiplus.com", settings, StringComparison.Ordinal);
        Assert.DoesNotContain("\"AllowedOrigins\": [\n      \"*\"", settings, StringComparison.Ordinal);
    }

    private static IHost BuildDataProtectionHost(Dictionary<string, string?> values)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(values).Build();
        return new HostBuilder()
            .ConfigureServices(services =>
            {
                services.AddLogging();
                services.AddCreditPaymentsDataProtection(configuration);
            })
            .Build();
    }

    private static Dictionary<string, string?> PersistentDataProtectionConfiguration(
        string keyRingPath,
        string certificatePath,
        string certificatePassword) => new()
    {
        ["ConnectionStrings:Default"] = "Host=localhost;Database=unused;Username=unused;Password=unused",
        ["CreditPayments:DataProtection:RequirePersistentKeyRing"] = "true",
        ["CreditPayments:DataProtection:ApplicationName"] = CreditPaymentsDataProtectionOptions.SharedApplicationName,
        ["CreditPayments:DataProtection:KeyRingPath"] = keyRingPath,
        ["CreditPayments:DataProtection:ProtectionCertificatePath"] = certificatePath,
        ["CreditPayments:DataProtection:ProtectionCertificatePassword"] = certificatePassword
    };

    private static string CreateTemporaryDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "refahi-cpg-dp-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private static void WriteTestCertificate(string path, string password)
    {
        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest("CN=Refahi CPG test key", rsa,
            HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        using var certificate = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-1),
            DateTimeOffset.UtcNow.AddDays(1));
        File.WriteAllBytes(path, certificate.Export(X509ContentType.Pfx, password));
    }

    private static ServiceProvider BuildApplicationProvider(Dictionary<string, string?> values)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(values).Build();
        var services = new ServiceCollection();
        services.AddLogging();
        services.RegisterCreditPaymentsApplication(configuration);
        return services.BuildServiceProvider();
    }

    private static Dictionary<string, string?> ValidConfiguration() => new()
    {
        ["CreditPayments:GatewayPayment:MinimumExpirySeconds"] = "1",
        ["CreditPayments:GatewayPayment:DefaultExpirySeconds"] = "2",
        ["CreditPayments:GatewayPayment:MaximumExpirySeconds"] = "3",
        ["CreditPayments:Checkout:PublicBaseUrl"] = "https://pay.example.test",
        ["CreditPayments:Checkout:ActiveTokenKeyVersion"] = "1",
        ["CreditPayments:Checkout:TokenKeys:1"] = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)),
        ["CreditPayments:Checkout:OtpResendCooldownSeconds"] = "1",
        ["CreditPayments:Checkout:MaximumOtpSends"] = "1",
        ["CreditPayments:Checkout:MaximumOtpVerifyAttempts"] = "1",
        ["CreditPayments:Checkout:SyntheticChallengeExpirySeconds"] = "1",
        ["CreditPayments:MerchantPaymentPolicy:Policies:0:MerchantId"] = Guid.NewGuid().ToString(),
        ["CreditPayments:MerchantPaymentPolicy:Policies:0:TerminalId"] = Guid.NewGuid().ToString(),
        ["CreditPayments:MerchantPaymentPolicy:Policies:0:CategoryCode"] = "store",
        ["CreditPayments:MerchantPaymentPolicy:Policies:0:PolicyId"] = "policy",
        ["CreditPayments:MerchantPaymentPolicy:Policies:0:Version"] = "1",
        ["CreditPayments:MerchantPaymentPolicy:Policies:0:DestinationWalletId"] = Guid.NewGuid().ToString(),
        ["CreditPayments:MerchantPaymentPolicy:Policies:0:EffectiveAt"] = DateTimeOffset.UtcNow.ToString("O")
    };

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Refahi.Backend.slnx")))
            directory = directory.Parent;
        return directory?.FullName ?? throw new InvalidOperationException("Repository root not found.");
    }

    private sealed class StubReadinessChecker(CreditPaymentsProductionReadinessDto result)
        : ICreditPaymentsProductionReadinessChecker
    {
        public Task<CreditPaymentsProductionReadinessDto> CheckAsync(CancellationToken cancellationToken) =>
            Task.FromResult(result);
    }
}
