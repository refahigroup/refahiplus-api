namespace Refahi.Modules.CreditPayments.Tests;

public sealed class AdminPhase01ReadContractTests
{
    [Fact]
    public void Integration_event_backlog_endpoint_is_read_only_and_admin_only()
    {
        var source = File.ReadAllText(Source("Refahi.Modules.CreditPayments.Api", "OperationalAdminEndpoints.cs"));
        Assert.Contains("MapGet(\"/admin/integration-events\"", source);
        Assert.Contains("SearchAdminIntegrationEvents", source);
        Assert.Contains("RequireAuthorization(\"AdminOnly\")", source);
        Assert.DoesNotContain("MapPost(\"/admin/integration-events", source);
    }

    [Fact]
    public void Integration_event_admin_contract_does_not_expose_payload_or_lease_owner()
    {
        var source = File.ReadAllText(Source("Refahi.Modules.CreditPayments.Application.Contracts", "OperationalAdminContracts.cs"));
        var start = source.IndexOf("public sealed record AdminIntegrationEventDto", StringComparison.Ordinal);
        var end = source.IndexOf(';', start);
        var contract = source[start..end];
        Assert.DoesNotContain("Payload", contract);
        Assert.DoesNotContain("LeaseOwner", contract);
        Assert.Contains("LastFailureCode", contract);
    }

    private static string Source(string project, string file)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Refahi.Backend.slnx")))
            directory = directory.Parent;
        if (directory is null) throw new DirectoryNotFoundException("Backend repository root was not found.");
        return Path.Combine(directory.FullName, "src", project, file);
    }
}
