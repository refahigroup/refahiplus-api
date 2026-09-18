using Refahi.Modules.CreditPayments.Application.Contracts;

namespace Refahi.Modules.CreditPayments.Tests;

public sealed class Phase12SecurityTests
{
    [Fact]
    public void Reconciliation_admin_dtos_do_not_expose_secret_material()
    {
        var forbidden = new[] { "secret", "cipher", "otp", "checkouttoken", "authorization", "signing" };
        var types = new[] { typeof(ReconciliationRunDto), typeof(ReconciliationFindingDto) };
        foreach (var property in types.SelectMany(x => x.GetProperties()))
            Assert.DoesNotContain(forbidden, word => property.Name.Contains(word, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Reconciliation_is_conservatively_disabled_by_default()
    {
        var options = new ReconciliationOptions();
        Assert.False(options.Enabled);
        Assert.False(options.AutoRemediationEnabled);
        Assert.InRange(options.BatchSize, 1, 500);
    }

    [Fact]
    public void Reconciliation_source_has_no_new_financial_or_settlement_path()
    {
        var root = FindRepositoryRoot();
        var application = File.ReadAllText(Path.Combine(root, "src", "Refahi.Modules.CreditPayments.Application", "ReconciliationHandlers.cs"));
        var infrastructure = File.ReadAllText(Path.Combine(root, "src", "Refahi.Modules.CreditPayments.Infrastructure", "ReconciliationRepository.cs"));
        var source = application + infrastructure;
        Assert.DoesNotContain("ExecuteOrderPaymentCommand", source, StringComparison.Ordinal);
        Assert.DoesNotContain("IWalletPaymentExecutor", source, StringComparison.Ordinal);
        Assert.DoesNotContain("ReserveAsync", source, StringComparison.Ordinal);
        Assert.DoesNotContain("CaptureAsync", source, StringComparison.Ordinal);
        Assert.DoesNotContain("ReleaseAsync", source, StringComparison.Ordinal);
        Assert.DoesNotContain("RefundAsync", source, StringComparison.Ordinal);
        Assert.DoesNotContain("Settlement", source, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Reconciliation_endpoints_are_all_admin_only_and_no_generic_fix_exists()
    {
        var root = FindRepositoryRoot();
        var source = File.ReadAllText(Path.Combine(root, "src", "Refahi.Modules.CreditPayments.Api", "ReconciliationAdminEndpoints.cs"));
        Assert.Equal(6, Count(source, "RequireAuthorization(\"AdminOnly\")"));
        Assert.DoesNotContain("/fix", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("/resolve", source, StringComparison.OrdinalIgnoreCase);
    }

    private static int Count(string source, string value)
    {
        var count = 0; var position = 0;
        while ((position = source.IndexOf(value, position, StringComparison.Ordinal)) >= 0) { count++; position += value.Length; }
        return count;
    }
    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Refahi.Backend.slnx"))) directory = directory.Parent;
        return directory?.FullName ?? throw new InvalidOperationException("Repository root not found.");
    }
}
