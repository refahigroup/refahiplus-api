using System.Threading;
using System.Threading.Tasks;
using Refahi.Modules.Wallets.Application.Contracts.Features.CapturePaymentIntent;
using Refahi.Modules.Wallets.Application.Contracts.Features.CreatePaymentIntent;
using Refahi.Modules.Wallets.Application.Contracts.Features.RefundPayment;
using Refahi.Modules.Wallets.Application.Contracts.Features.ReleasePaymentIntent;

namespace Refahi.Modules.Wallets.Application.Contracts.Usecases;

/// <summary>
/// Stable application boundary for trusted modules that need Wallet to execute
/// financial mutations. Consumers must not access Wallet persistence or domain entities.
/// </summary>
public interface IWalletPaymentExecutor
{
    Task<CommandResponse<CreatePaymentIntentResponse>> ReserveAsync(
        CreatePaymentIntentCommand command,
        CancellationToken ct
    );

    Task<CommandResponse<CapturePaymentIntentResponse>> CaptureAsync(
        CapturePaymentIntentCommand command,
        CancellationToken ct
    );

    Task<CommandResponse<ReleasePaymentIntentResponse>> ReleaseAsync(
        ReleasePaymentIntentCommand command,
        CancellationToken ct
    );

    Task<CommandResponse<RefundPaymentResponse>> RefundAsync(
        RefundPaymentCommand command,
        CancellationToken ct
    );
}
