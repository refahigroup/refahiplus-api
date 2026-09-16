using System;
using System.Threading;
using System.Threading.Tasks;

namespace Refahi.Modules.PaymentGateway.Application.Contracts.Funding;

/// <summary>
/// Stable cross-context boundary for requesting and observing external Wallet funding.
/// It deliberately hides PSP callbacks, tokens, reference numbers and persistence.
/// </summary>
public interface IWalletFundingService
{
    Task<CreateWalletFundingResult> CreateAsync(
        CreateWalletFundingRequest request,
        CancellationToken ct = default);
    Task<WalletFundingStatus?> GetStatusAsync(
        Guid userId,
        Guid fundingSessionId,
        CancellationToken ct = default);
}

public sealed record CreateWalletFundingRequest(
    Guid UserId,
    Guid WalletId,
    long AmountMinor,
    string Currency,
    string FundingProvider,
    string ProviderCallbackUrl,
    string ReturnBaseUrl,
    string IdempotencyKey,
    string? SucceededCallbackUrl = null,
    string? FailedCallbackUrl = null);

public sealed record CreateWalletFundingResult(Guid FundingSessionId, string RedirectUrl);

public sealed record WalletFundingStatus(
    Guid FundingSessionId,
    Guid WalletId,
    long AmountMinor,
    string Currency,
    string Status,
    Guid? WalletLedgerEntryId);
