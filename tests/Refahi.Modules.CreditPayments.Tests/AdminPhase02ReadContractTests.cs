namespace Refahi.Modules.CreditPayments.Tests;

public sealed class AdminPhase02ReadContractTests
{
    [Fact]
    public void Merchant_and_terminal_lists_are_read_only_and_admin_only()
    {
        var source = File.ReadAllText(Source("Refahi.Modules.CreditPayments.Api", "OperationalAdminEndpoints.cs"));
        Assert.Contains("MapGet(\"/admin/merchants\"", source);
        Assert.Contains("MapGet(\"/admin/terminals\"", source);
        Assert.Contains("SearchAdminMerchantsQuery", source);
        Assert.Contains("SearchAdminTerminalsQuery", source);
        Assert.DoesNotContain("MapDelete(\"/admin/merchants", source);
        Assert.DoesNotContain("MapDelete(\"/admin/terminals/{id", source);
    }

    [Fact]
    public void Phase02_admin_contracts_do_not_expose_secret_material()
    {
        var source = File.ReadAllText(Source("Refahi.Modules.CreditPayments.Application.Contracts", "OperationalAdminContracts.cs"));
        Assert.DoesNotContain("SecretHash", source);
        Assert.DoesNotContain("SigningSecretCiphertext", source);
        Assert.Contains("AdminCredentialMetadataDto", source);
    }

    [Fact]
    public void Risk_and_audit_filters_remain_read_only()
    {
        var source = File.ReadAllText(Source("Refahi.Modules.CreditPayments.Api", "OperationalAdminEndpoints.cs"));
        Assert.Contains("MapGet(\"/admin/audit\"", source);
        Assert.Contains("MapGet(\"/admin/risk-decisions\"", source);
        Assert.DoesNotContain("MapPost(\"/admin/risk", source);
        Assert.DoesNotContain("MapDelete(\"/admin/audit", source);
    }

    private static string Source(string project, string file)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Refahi.Backend.slnx"))) directory = directory.Parent;
        if (directory is null) throw new DirectoryNotFoundException("Backend repository root was not found.");
        return Path.Combine(directory.FullName, "src", project, file);
    }
}
