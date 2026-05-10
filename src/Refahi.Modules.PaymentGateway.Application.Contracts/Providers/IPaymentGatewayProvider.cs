using Refahi.Modules.PaymentGateway.Domain.Enums;
using System.Threading;
using System.Threading.Tasks;

namespace Refahi.Modules.PaymentGateway.Application.Contracts.Providers;

/// <summary>
/// Unified abstraction for all payment gateway providers.
/// Each provider (SEP, Mellat, Parsian, etc.) implements this interface.
/// </summary>
public interface IPaymentGatewayProvider
{
    PaymentGatewayProviderType ProviderType { get; }

    /// <summary>
    /// Step 1: Request a payment token from the provider.
    /// </summary>
    Task<GetTokenResult> GetTokenAsync(GetTokenRequest request, CancellationToken ct = default);

    /// <summary>
    /// Builds the URL to redirect the user's browser to the provider's payment page.
    /// </summary>
    string BuildRedirectUrl(string token);

    /// <summary>
    /// Step 3: Verify the transaction after the provider's callback.
    /// Must be called before crediting the wallet (prevents double-spending).
    /// </summary>
    Task<VerifyResult> VerifyAsync(VerifyRequest request, CancellationToken ct = default);
}
