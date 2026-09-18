using System.Net;
using System.Text;
using Microsoft.Extensions.Options;
using Refahi.Modules.CreditPayments.Application.Contracts;
using Refahi.Modules.CreditPayments.Infrastructure;

namespace Refahi.Modules.CreditPayments.Tests;

public sealed class Phase10SecurityTests
{
    [Theory]
    [InlineData("127.0.0.1")]
    [InlineData("0.0.0.0")]
    [InlineData("10.1.2.3")]
    [InlineData("172.16.0.1")]
    [InlineData("192.168.1.1")]
    [InlineData("169.254.169.254")]
    [InlineData("100.64.0.1")]
    [InlineData("192.0.2.1")]
    [InlineData("198.51.100.1")]
    [InlineData("203.0.113.1")]
    [InlineData("::1")]
    [InlineData("fe80::1")]
    [InlineData("fc00::1")]
    [InlineData("2001:db8::1")]
    public void Unsafe_literal_addresses_are_blocked(string value) =>
        Assert.False(WebhookIpPolicy.IsPublic(IPAddress.Parse(value)));

    [Theory]
    [InlineData("8.8.8.8")]
    [InlineData("1.1.1.1")]
    [InlineData("2606:4700:4700::1111")]
    public void Public_addresses_are_allowed(string value) =>
        Assert.True(WebhookIpPolicy.IsPublic(IPAddress.Parse(value)));

    [Fact]
    public async Task Mixed_dns_answer_rejects_entire_endpoint()
    {
        var validator = Validator(new FakeDns(IPAddress.Parse("8.8.8.8"), IPAddress.Parse("10.0.0.1")));
        var error = await Assert.ThrowsAsync<WebhookSecurityException>(() =>
            validator.ValidateAsync("https://merchant.example/hook", default));
        Assert.Equal("endpoint_address_blocked", error.FailureCode);
    }

    [Fact]
    public async Task Disallowed_port_is_rejected_before_dns()
    {
        var dns = new FakeDns(IPAddress.Parse("8.8.8.8"));
        var error = await Assert.ThrowsAsync<WebhookSecurityException>(() =>
            Validator(dns).ValidateAsync("https://merchant.example:8443/hook", default));
        Assert.Equal("endpoint_port_blocked", error.FailureCode);
        Assert.Equal(0, dns.Calls);
    }

    [Theory]
    [InlineData("http://merchant.example/hook")]
    [InlineData("https://user:pass@merchant.example/hook")]
    [InlineData("https://merchant.example/hook#fragment")]
    [InlineData("https://localhost/hook")]
    public async Task Unsafe_url_shapes_are_rejected(string url) =>
        await Assert.ThrowsAsync<WebhookSecurityException>(() => Validator(new FakeDns()).ValidateAsync(url, default));

    [Fact]
    public void Hmac_has_frozen_deterministic_vector()
    {
        var id = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var signature = WebhookSignature.Create("test-secret", id, 1_700_000_000,
            Encoding.UTF8.GetBytes("{\"ok\":true}"));
        Assert.Equal("v1=aea83f7ed167ff2152f331f0af4d51caf15b0ce2f705380b994ee07e8095fa23", signature);
    }

    [Fact]
    public void Changing_body_or_timestamp_changes_signature()
    {
        var id = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var original = WebhookSignature.Create("test-secret", id, 1_700_000_000, Encoding.UTF8.GetBytes("a"));
        Assert.NotEqual(original, WebhookSignature.Create("test-secret", id, 1_700_000_000, Encoding.UTF8.GetBytes("b")));
        Assert.NotEqual(original, WebhookSignature.Create("test-secret", id, 1_700_000_001, Encoding.UTF8.GetBytes("a")));
    }

    [Fact]
    public void Production_transport_pins_connection_and_keeps_tls_defaults()
    {
        var root = FindRepositoryRoot();
        var source = File.ReadAllText(Path.Combine(root,
            "src", "Refahi.Modules.CreditPayments.Infrastructure", "WebhookDeliveryServices.cs"));
        Assert.Contains("AllowAutoRedirect = false", source);
        Assert.Contains("UseProxy = false", source);
        Assert.Contains("ConnectCallback", source);
        Assert.DoesNotContain("ServerCertificateCustomValidationCallback", source);
        Assert.DoesNotContain("DangerousAcceptAnyServerCertificateValidator", source);
    }

    private static WebhookEndpointSecurityValidator Validator(IWebhookDnsResolver dns) =>
        new(dns, Options.Create(new WebhookOptions { AllowedPorts = [443] }));

    private sealed class FakeDns(params IPAddress[] addresses) : IWebhookDnsResolver
    {
        public int Calls { get; private set; }
        public Task<IReadOnlyList<IPAddress>> ResolveAsync(string host, CancellationToken ct)
        { Calls++; return Task.FromResult<IReadOnlyList<IPAddress>>(addresses); }
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Refahi.Backend.slnx")))
            directory = directory.Parent;
        return directory?.FullName ?? throw new InvalidOperationException("Repository root not found.");
    }
}
