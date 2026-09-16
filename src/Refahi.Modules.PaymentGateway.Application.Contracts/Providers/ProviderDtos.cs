using Refahi.Modules.PaymentGateway.Domain.Enums;

namespace Refahi.Modules.PaymentGateway.Application.Contracts.Providers;

public sealed record GetTokenRequest(
    /// <summary>Session ID used as ResNum / reference number for the provider.</summary>
    string ResNum,
    long AmountMinor,
    /// <summary>Backend callback URL where the provider will POST the result.</summary>
    string CallbackUrl,
    string? CellNumber = null
);

public sealed record GetTokenResult(
    bool IsSuccess,
    string? Token,
    string? ProviderAttemptId = null,
    string? ProviderTransactionId = null,
    string? ErrorMessage = null,
    bool IsOutcomeUnknown = false);

public sealed record VerifyRequest(
    string ProviderTransactionId,
    string ProviderAttemptId,
    string SessionReference,
    long ExpectedAmountMinor,
    string ExpectedCurrency);

public sealed record VerifyResult(
    FundingProviderOutcome Outcome,
    string? ProviderTransactionId,
    long? VerifiedAmountMinor,
    string? VerifiedCurrency,
    string? ClientReference,
    int ResultCode,
    string? ErrorMessage = null
)
{
    public bool IsSuccess => Outcome == FundingProviderOutcome.VerifiedSuccess;
}

public sealed record FundingInquiryRequest(
    string? ProviderAttemptId,
    string? ProviderTransactionId,
    string SessionReference,
    long ExpectedAmountMinor,
    string ExpectedCurrency);

public sealed record ReverseRequest(string RefNum);

public sealed record ReverseResult(bool IsSuccess, int ResultCode, string? ErrorMessage = null);
