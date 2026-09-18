using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using Refahi.Modules.CreditPayments.Application.Contracts;

namespace Refahi.Modules.CreditPayments.Infrastructure;

public sealed class ConfigurationMerchantPaymentPolicyResolver(
    IOptionsMonitor<MerchantPaymentPolicyOptions> configured)
    : IMerchantPaymentPolicyResolver
{
    public Task<MerchantPaymentPolicy?> ResolveAsync(
        Guid merchantId,
        Guid terminalId,
        string approvedCategoryCode,
        long amountMinor,
        string currency,
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (amountMinor <= 0 || !string.Equals(currency, "IRR", StringComparison.Ordinal))
            return Task.FromResult<MerchantPaymentPolicy?>(null);

        var matches = configured.CurrentValue.Policies
            .Where(x => (!x.MerchantId.HasValue || x.MerchantId == merchantId)
                && (!x.TerminalId.HasValue || x.TerminalId == terminalId)
                && string.Equals(x.CategoryCode?.Trim(), approvedCategoryCode, StringComparison.Ordinal))
            .OrderByDescending(x => x.TerminalId.HasValue)
            .ThenByDescending(x => x.MerchantId.HasValue)
            .ToArray();
        if (matches.Length != 1)
            return Task.FromResult<MerchantPaymentPolicy?>(null);

        var policy = matches[0];
        if (string.IsNullOrWhiteSpace(policy.PolicyId)
            || policy.PolicyId.Trim().Length > 100
            || string.IsNullOrWhiteSpace(policy.Version)
            || policy.Version.Trim().Length > 100
            || policy.DestinationWalletId == Guid.Empty
            || policy.EffectiveAt == default)
            return Task.FromResult<MerchantPaymentPolicy?>(null);

        var canonical = string.Join("\n",
            "cpg-payment-policy-v1",
            policy.PolicyId.Trim(),
            policy.Version.Trim(),
            approvedCategoryCode,
            policy.DestinationWalletId.ToString("D"),
            policy.EffectiveAt.ToUniversalTime().ToString("O"));
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical))).ToLowerInvariant();
        return Task.FromResult<MerchantPaymentPolicy?>(new(
            policy.PolicyId.Trim(),
            policy.Version.Trim(),
            hash,
            policy.EffectiveAt,
            approvedCategoryCode,
            policy.DestinationWalletId));
    }
}
