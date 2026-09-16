using System;
using System.Security.Cryptography;
using System.Text;
using Refahi.Modules.PaymentGateway.Application.Contracts.Features.InitiatePayment;

namespace Refahi.Modules.PaymentGateway.Application.Features.InitiatePayment;

internal static class FundingInitiationHasher
{
    public const short Version = 1;

    public static string Compute(InitiatePaymentCommand command)
    {
        var canonical = string.Join('\n',
            command.UserId.ToString("D"),
            command.WalletId.ToString("D"),
            command.AmountMinor.ToString(System.Globalization.CultureInfo.InvariantCulture),
            command.Currency.Trim().ToUpperInvariant(),
            ((short)command.Provider).ToString(System.Globalization.CultureInfo.InvariantCulture),
            Normalize(command.ReturnBaseUrl),
            Normalize(command.SucceededCallbackUrl),
            Normalize(command.FailedCallbackUrl));
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical))).ToLowerInvariant();
    }

    private static string Normalize(string? value) => value?.Trim() ?? "<null>";
}
