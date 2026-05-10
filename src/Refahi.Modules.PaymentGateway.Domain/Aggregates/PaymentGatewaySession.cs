using Refahi.Modules.PaymentGateway.Domain.Common;
using Refahi.Modules.PaymentGateway.Domain.Enums;
using Refahi.Modules.PaymentGateway.Domain.Events;
using Refahi.Modules.PaymentGateway.Domain.Exceptions;
using System;

namespace Refahi.Modules.PaymentGateway.Domain.Aggregates;

/// <summary>
/// Aggregate Root: PaymentGatewaySession
///
/// Represents a single top-up payment session from initiation through
/// provider redirect to final settlement.
///
/// State Machine:
///   Initiated → TokenReceived → Redirected → CallbackReceived → Succeeded
///                                                             → Failed
///   Initiated → Failed  (if token request fails)
///   Any non-terminal → Expired (if ExpiresAt has passed)
/// </summary>
public sealed class PaymentGatewaySession : EntityBase
{
    // Parameterless ctor for EF Core
    private PaymentGatewaySession() { }

    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public Guid WalletId { get; private set; }
    public long AmountMinor { get; private set; }
    public string Currency { get; private set; } = default!;
    public PaymentGatewayProviderType Provider { get; private set; }
    public PaymentSessionStatus Status { get; private set; }

    /// <summary>
    /// Base URL for the Blazor result page. Backend appends /{Id} to build the final redirect.
    /// e.g. "https://app.refahi.ir/charge/wallet/topup/result"
    /// </summary>
    public string ReturnBaseUrl { get; private set; } = default!;

    /// <summary>Optional: redirect here on success (overrides ReturnBaseUrl/{Id}).</summary>
    public string? SucceededCallbackUrl { get; private set; }

    /// <summary>Optional: redirect here on failure (overrides ReturnBaseUrl/{Id}).</summary>
    public string? FailedCallbackUrl { get; private set; }

    public string? ProviderToken { get; private set; }
    public string? ProviderRefNum { get; private set; }
    public string? ProviderTraceNo { get; private set; }
    public string? ProviderSecurePan { get; private set; }
    public string? ProviderRawCallbackJson { get; private set; }
    public int? ProviderResultCode { get; private set; }
    public string? ProviderResultDescription { get; private set; }

    public DateTimeOffset InitiatedAt { get; private set; }
    public DateTimeOffset ExpiresAt { get; private set; }
    public DateTimeOffset? CompletedAt { get; private set; }

    /// <summary>LedgerEntryId from the Wallets module after successful top-up.</summary>
    public Guid? TopUpLedgerEntryId { get; private set; }

    // ─────────────────────────────────────────────────────────
    // Factory
    // ─────────────────────────────────────────────────────────

    public static PaymentGatewaySession Create(
        Guid sessionId,
        Guid userId,
        Guid walletId,
        long amountMinor,
        string currency,
        PaymentGatewayProviderType provider,
        string returnBaseUrl,
        int expiryMinutes = 15,
        string? succeededCallbackUrl = null,
        string? failedCallbackUrl = null)
    {
        if (amountMinor <= 0)
            throw new InvalidPaymentAmountException("مبلغ پرداخت باید بزرگتر از صفر باشد.");

        if (string.IsNullOrWhiteSpace(returnBaseUrl))
            throw new ArgumentException("ReturnBaseUrl الزامی است.", nameof(returnBaseUrl));

        var now = DateTimeOffset.UtcNow;
        return new PaymentGatewaySession
        {
            Id = sessionId,
            UserId = userId,
            WalletId = walletId,
            AmountMinor = amountMinor,
            Currency = currency.ToUpperInvariant(),
            Provider = provider,
            Status = PaymentSessionStatus.Initiated,
            ReturnBaseUrl = returnBaseUrl,
            SucceededCallbackUrl = succeededCallbackUrl,
            FailedCallbackUrl = failedCallbackUrl,
            InitiatedAt = now,
            ExpiresAt = now.AddMinutes(expiryMinutes)
        };
    }

    // ─────────────────────────────────────────────────────────
    // State transitions
    // ─────────────────────────────────────────────────────────

    public void MarkAsTokenReceived(string token)
    {
        EnsureNotTerminal();
        EnsureNotExpired();
        if (Status != PaymentSessionStatus.Initiated)
            throw new InvalidPaymentSessionStateException($"صدور توکن فقط از وضعیت Initiated امکان‌پذیر است. وضعیت فعلی: {Status}");

        ProviderToken = token;
        Status = PaymentSessionStatus.TokenReceived;
    }

    public void MarkAsRedirected()
    {
        EnsureNotTerminal();
        EnsureNotExpired();
        Status = PaymentSessionStatus.Redirected;
    }

    public void MarkAsCallbackReceived(
        string state,
        string? refNum,
        string? traceNo,
        string? securePan,
        string? rawCallbackJson)
    {
        EnsureNotExpired();

        if (Status is PaymentSessionStatus.Succeeded or PaymentSessionStatus.Failed)
            throw new PaymentSessionAlreadyProcessedException(
                "این جلسه پرداخت قبلاً پردازش شده است.");

        ProviderRefNum = refNum;
        ProviderTraceNo = traceNo;
        ProviderSecurePan = securePan;
        ProviderRawCallbackJson = rawCallbackJson;
        Status = PaymentSessionStatus.CallbackReceived;
    }

    public void MarkAsSucceeded(Guid topUpLedgerEntryId, int providerResultCode, string? providerResultDescription = null)
    {
        Status = PaymentSessionStatus.Succeeded;
        TopUpLedgerEntryId = topUpLedgerEntryId;
        ProviderResultCode = providerResultCode;
        ProviderResultDescription = providerResultDescription;
        CompletedAt = DateTimeOffset.UtcNow;

        AddDomainEvent(new PaymentSessionSucceededDomainEvent(
            SessionId: Id,
            UserId: UserId,
            WalletId: WalletId,
            AmountMinor: AmountMinor,
            Currency: Currency,
            Provider: Provider,
            TopUpLedgerEntryId: topUpLedgerEntryId,
            OccurredAt: CompletedAt.Value));
    }

    public void MarkAsFailed(int? providerResultCode, string? providerResultDescription)
    {
        Status = PaymentSessionStatus.Failed;
        ProviderResultCode = providerResultCode;
        ProviderResultDescription = providerResultDescription;
        CompletedAt = DateTimeOffset.UtcNow;

        AddDomainEvent(new PaymentSessionFailedDomainEvent(
            SessionId: Id,
            UserId: UserId,
            WalletId: WalletId,
            AmountMinor: AmountMinor,
            Currency: Currency,
            Provider: Provider,
            FailureReason: providerResultDescription,
            OccurredAt: CompletedAt.Value));
    }

    public void MarkAsExpired()
    {
        if (IsTerminal())
            return;

        Status = PaymentSessionStatus.Expired;
        CompletedAt = DateTimeOffset.UtcNow;
    }

    // ─────────────────────────────────────────────────────────
    // Helpers
    // ─────────────────────────────────────────────────────────

    public bool IsExpired() => DateTimeOffset.UtcNow > ExpiresAt && !IsTerminal();

    public bool IsTerminal() =>
        Status is PaymentSessionStatus.Succeeded
            or PaymentSessionStatus.Failed
            or PaymentSessionStatus.Expired;

    public string BuildSuccessRedirectUrl() =>
        SucceededCallbackUrl ?? $"{ReturnBaseUrl.TrimEnd('/')}/{Id}";

    public string BuildFailureRedirectUrl() =>
        FailedCallbackUrl ?? $"{ReturnBaseUrl.TrimEnd('/')}/{Id}";

    private void EnsureNotExpired()
    {
        if (IsExpired())
            throw new PaymentSessionExpiredException("مدت زمان جلسه پرداخت منقضی شده است.");
    }

    private void EnsureNotTerminal()
    {
        if (IsTerminal())
            throw new InvalidPaymentSessionStateException($"جلسه پرداخت در وضعیت نهایی {Status} قرار دارد و قابل تغییر نیست.");
    }
}
