namespace Refahi.Modules.CreditPayments.Tests;

public sealed class HostedCheckoutExtractionContractTests
{
    private static readonly string Root = FindRepositoryRoot();

    [Fact]
    public void Checkout_endpoints_require_dedicated_tight_cors_policy()
    {
        var endpoints = File.ReadAllText(Path.Combine(Root, "src", "Refahi.Modules.CreditPayments.Api", "PaymentAndCheckoutEndpoints.cs"));
        var checkoutSection = endpoints[(endpoints.IndexOf("public sealed class CheckoutEndpoints", StringComparison.Ordinal))..];
        Assert.Equal(8, Count(checkoutSection, ".RequireCors(DI.CheckoutCorsPolicy)"));

        var module = File.ReadAllText(Path.Combine(Root, "src", "Refahi.Modules.CreditPayments.Api", "DI.cs"));
        Assert.Contains("WithMethods(\"GET\", \"POST\", \"OPTIONS\")", module, StringComparison.Ordinal);
        Assert.Contains("WithHeaders(\"Accept\", \"Content-Type\", \"Idempotency-Key\")", module, StringComparison.Ordinal);
        Assert.Contains("AllowCredentials()", module, StringComparison.Ordinal);
    }

    [Fact]
    public void Production_checkout_origin_is_pay_only_and_cookie_stays_host_only()
    {
        var settings = File.ReadAllText(Path.Combine(Root, "src", "Refahi.Api", "appsettings.json"));
        Assert.Contains("\"AllowedOrigins\": [\n        \"https://pay.refahiplus.com\"", settings.Replace("\r\n", "\n"), StringComparison.Ordinal);

        var endpoints = File.ReadAllText(Path.Combine(Root, "src", "Refahi.Modules.CreditPayments.Api", "PaymentAndCheckoutEndpoints.cs"));
        Assert.Contains("__Host-refahi-cpg-checkout", endpoints, StringComparison.Ordinal);
        Assert.DoesNotContain("Domain =", endpoints, StringComparison.Ordinal);
        Assert.Contains("Secure = true", endpoints, StringComparison.Ordinal);
        Assert.Contains("HttpOnly = true", endpoints, StringComparison.Ordinal);
    }

    private static int Count(string source, string value) => source.Split(value, StringSplitOptions.None).Length - 1;

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Refahi.Backend.slnx"))) directory = directory.Parent;
        return directory?.FullName ?? throw new DirectoryNotFoundException("Backend repository root was not found.");
    }
}
