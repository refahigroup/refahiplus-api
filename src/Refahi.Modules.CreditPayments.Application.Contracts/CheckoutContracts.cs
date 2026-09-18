using MediatR;

namespace Refahi.Modules.CreditPayments.Application.Contracts;

public sealed class CheckoutOptions
{
    public const string SectionName = "CreditPayments:Checkout";
    public string PublicBaseUrl { get; set; } = string.Empty;
    public int ActiveTokenKeyVersion { get; set; } = 1;
    public Dictionary<int, string> TokenKeys { get; set; } = [];
    public int OtpResendCooldownSeconds { get; set; } = 60;
    public int MaximumOtpSends { get; set; } = 5;
    public int MaximumOtpVerifyAttempts { get; set; } = 5;
    public int SyntheticChallengeExpirySeconds { get; set; } = 300;
}

public sealed record GetCheckoutQuery(string Token) : IRequest<CheckoutDto?>;

public sealed record SendCheckoutOtpCommand(string Token, string Identifier)
    : IRequest<CheckoutOtpChallengeDto>;

public sealed record VerifyCheckoutOtpCommand(
    string Token,
    Guid ChallengeId,
    string Otp,
    string? ExistingAuthorizationSecret)
    : IRequest<CheckoutOtpVerificationResult>;

public sealed record CheckoutDto(
    string PaymentStatus,
    string MerchantDisplayName,
    string? TerminalDisplayName,
    long AmountMinor,
    string Currency,
    string? Description,
    DateTimeOffset ExpiresAt,
    string AuthenticationStatus,
    DateTimeOffset? PaidAt = null,
    string? ReturnUrl = null);

public sealed record CheckoutOtpChallengeDto(
    Guid ChallengeId,
    DateTimeOffset ExpiresAt,
    DateTimeOffset ResendAfter,
    string Message);

public sealed record CheckoutOtpVerificationResult(
    string AuthenticationStatus,
    string? AuthorizationSecret,
    DateTimeOffset? AuthorizationExpiresAt,
    bool AlreadyAuthenticated);
