namespace Refahi.Modules.PaymentGateway.Application.Contracts.Providers;

public sealed record GetTokenRequest(
    /// <summary>Session ID used as ResNum / reference number for the provider.</summary>
    string ResNum,
    long AmountMinor,
    /// <summary>Backend callback URL where the provider will POST the result.</summary>
    string CallbackUrl,
    string? CellNumber = null);

public sealed record GetTokenResult(
    bool IsSuccess,
    string? Token,
    string? ErrorMessage = null);

public sealed record VerifyRequest(
    string RefNum,
    long ExpectedAmountMinor);

public sealed record VerifyResult(
    bool IsSuccess,
    long VerifiedAmountMinor,
    int ResultCode,
    string? ErrorMessage = null);
