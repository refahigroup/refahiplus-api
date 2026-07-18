using System.Text.Json;
using Refahi.Modules.Charge.Infrastructure.Providers.Eniac;

namespace Refahi.Modules.Charge.Tests;

public sealed class ProviderPayloadSanitizerTests
{
    [Fact]
    public void Recursively_redacts_secrets_and_masks_mobile_numbers()
    {
        var result = ProviderPayloadSanitizer.SanitizeJson("""
            {"authorization":"Bearer secret","nested":{"token":"abc","password":"p","pin":"1234","serial":"s","mobile":"09121234567"}}
            """);

        Assert.DoesNotContain("secret", result);
        Assert.DoesNotContain("abc", result);
        Assert.DoesNotContain("1234", result);
        Assert.DoesNotContain("09121234567", result);
        Assert.Contains("0912***4567", result);
        using var _ = JsonDocument.Parse(result);
    }

    [Fact]
    public void Oversized_or_invalid_payload_is_valid_json_within_limit()
    {
        var result = ProviderPayloadSanitizer.SanitizeJson("not-json-" + new string('x', 30_000));

        Assert.True(result.Length <= ProviderPayloadSanitizer.MaxSnapshotLength);
        using var document = JsonDocument.Parse(result);
        Assert.True(document.RootElement.GetProperty("truncated").GetBoolean());
    }
}
