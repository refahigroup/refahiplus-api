using System;
using Refahi.Modules.PaymentGateway.Domain.Common;
using Refahi.Modules.PaymentGateway.Domain.Enums;
using Refahi.Modules.PaymentGateway.Domain.Events;
using Refahi.Modules.PaymentGateway.Domain.Exceptions;

namespace Refahi.Modules.PaymentGateway.Domain.Aggregates;

/// <summary>One external PSP attempt whose only purpose is funding one Wallet.</summary>
public sealed class PaymentGatewaySession : EntityBase
{
    private const int DescriptionMaxLength = 500;
    private PaymentGatewaySession() { }

    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public Guid WalletId { get; private set; }
    public long AmountMinor { get; private set; }
    public string Currency { get; private set; } = default!;
    public PaymentGatewayProviderType Provider { get; private set; }
    public PaymentSessionStatus Status { get; private set; }
    public string ReturnBaseUrl { get; private set; } = default!;
    public string? SucceededCallbackUrl { get; private set; }
    public string? FailedCallbackUrl { get; private set; }
    public string? ProviderToken { get; private set; }
    public string? ProviderAttemptId { get; private set; }
    public string? ProviderTransactionId { get; private set; }
    public string? ProviderRefNum { get; private set; }
    public string? ProviderTraceNo { get; private set; }
    public string? ProviderSecurePan { get; private set; }
    public string? ProviderRawCallbackJson { get; private set; }
    public int? ProviderResultCode { get; private set; }
    public string? ProviderResultDescription { get; private set; }
    public string? InitiationIdempotencyKey { get; private set; }
    public string? InitiationRequestHash { get; private set; }
    public short? InitiationRequestHashVersion { get; private set; }
    public DateTimeOffset InitiatedAt { get; private set; }
    public DateTimeOffset ExpiresAt { get; private set; }
    public DateTimeOffset? CompletedAt { get; private set; }
    public DateTimeOffset? ProcessingLeaseUntil { get; private set; }
    public DateTimeOffset? LastRecoveryAt { get; private set; }
    public DateTimeOffset? NextRecoveryAt { get; private set; }
    public int RecoveryAttemptCount { get; private set; }
    public Guid? TopUpLedgerEntryId { get; private set; }
    public FundingReversalStatus ReversalStatus { get; private set; }
    public int ReversalAttemptCount { get; private set; }
    public DateTimeOffset? NextReversalAt { get; private set; }
    public string? ReversalDescription { get; private set; }
    public long Version { get; private set; }

    public static PaymentGatewaySession Create(
        Guid sessionId, Guid userId, Guid walletId, long amountMinor, string currency,
        PaymentGatewayProviderType provider, string returnBaseUrl, int expiryMinutes = 15,
        string? succeededCallbackUrl = null, string? failedCallbackUrl = null,
        string? initiationIdempotencyKey = null, string? initiationRequestHash = null,
        short? initiationRequestHashVersion = null)
    {
        if (amountMinor <= 0)
            throw new InvalidPaymentAmountException("مبلغ پرداخت باید بزرگتر از صفر باشد.");
        if (string.IsNullOrWhiteSpace(returnBaseUrl))
            throw new ArgumentException("ReturnBaseUrl الزامی است.", nameof(returnBaseUrl));
        if ((initiationRequestHash is null) != (initiationRequestHashVersion is null))
            throw new ArgumentException("هش و نسخه درخواست باید هم‌زمان ثبت شوند.");

        var now = DateTimeOffset.UtcNow;
        return new PaymentGatewaySession
        {
            Id = sessionId, UserId = userId, WalletId = walletId,
            AmountMinor = amountMinor, Currency = currency.ToUpperInvariant(),
            Provider = provider, Status = PaymentSessionStatus.Initiated,
            ReturnBaseUrl = returnBaseUrl, SucceededCallbackUrl = succeededCallbackUrl,
            FailedCallbackUrl = failedCallbackUrl,
            InitiationIdempotencyKey = initiationIdempotencyKey,
            InitiationRequestHash = initiationRequestHash,
            InitiationRequestHashVersion = initiationRequestHashVersion,
            InitiatedAt = now, ExpiresAt = now.AddMinutes(expiryMinutes),
            ReversalStatus = FundingReversalStatus.None,
        };
    }

    public void MarkAsTokenReceived(string token, string providerAttemptId,
        string? providerTransactionId = null)
    {
        EnsureNotTerminal();
        if (Status != PaymentSessionStatus.Initiated)
            throw new InvalidPaymentSessionStateException(
                $"صدور توکن فقط از وضعیت Initiated امکان‌پذیر است. وضعیت فعلی: {Status}");
        if (string.IsNullOrWhiteSpace(providerAttemptId))
            throw new ArgumentException("شناسه تلاش درگاه الزامی است.", nameof(providerAttemptId));
        ProviderToken = token;
        ProviderAttemptId = providerAttemptId;
        ProviderTransactionId = providerTransactionId;
        Status = PaymentSessionStatus.TokenReceived;
    }

    public void RecordCallback(string? refNum, string? traceNo, string? securePan,
        string? rawCallbackJson)
    {
        EnsureNotTerminal();
        ProviderRefNum = refNum;
        ProviderTraceNo = traceNo;
        ProviderSecurePan = securePan;
        ProviderRawCallbackJson = rawCallbackJson;
    }

    public void BindProviderTransaction(string providerTransactionId)
    {
        if (string.IsNullOrWhiteSpace(providerTransactionId))
            throw new ArgumentException("شناسه تراکنش درگاه الزامی است.", nameof(providerTransactionId));
        if (ProviderTransactionId is not null &&
            !string.Equals(ProviderTransactionId, providerTransactionId, StringComparison.Ordinal))
            throw new InvalidPaymentSessionStateException("شناسه تراکنش با تلاش ثبت‌شده تطابق ندارد.");
        ProviderTransactionId = providerTransactionId;
    }

    public void BindRecoveredProviderIdentity(string providerAttemptId,
        string providerTransactionId)
    {
        EnsureNotTerminal();
        if (string.IsNullOrWhiteSpace(providerAttemptId) ||
            string.IsNullOrWhiteSpace(providerTransactionId))
            throw new ArgumentException("شناسه بازیابی‌شده درگاه الزامی است.");
        if (ProviderAttemptId is not null &&
            !string.Equals(ProviderAttemptId, providerAttemptId, StringComparison.Ordinal))
            throw new InvalidPaymentSessionStateException("شناسه تلاش بازیابی‌شده با جلسه تطابق ندارد.");
        ProviderAttemptId = providerAttemptId;
        BindProviderTransaction(providerTransactionId);
    }

    public void MarkPendingRecovery(int? code, string? description, DateTimeOffset nextRecoveryAt)
    {
        EnsureNotTerminal();
        Status = PaymentSessionStatus.PendingRecovery;
        ProviderResultCode = code;
        ProviderResultDescription = Truncate(description);
        ProcessingLeaseUntil = null;
        LastRecoveryAt = DateTimeOffset.UtcNow;
        NextRecoveryAt = nextRecoveryAt;
        RecoveryAttemptCount++;
    }

    public void MarkAsSucceeded(Guid ledgerEntryId, int code, string? description = null)
    {
        EnsureNotTerminal();
        Status = PaymentSessionStatus.Succeeded;
        TopUpLedgerEntryId = ledgerEntryId;
        ProviderResultCode = code;
        ProviderResultDescription = Truncate(description);
        CompletedAt = DateTimeOffset.UtcNow;
        ProcessingLeaseUntil = null;
        NextRecoveryAt = null;
        AddDomainEvent(new PaymentSessionSucceededDomainEvent(
            Id, UserId, WalletId, AmountMinor, Currency, Provider, ledgerEntryId, CompletedAt.Value));
    }

    public void MarkAsFailed(int? code, string? description)
    {
        EnsureNotTerminal();
        Status = PaymentSessionStatus.Failed;
        ProviderResultCode = code;
        ProviderResultDescription = Truncate(description);
        CompletedAt = DateTimeOffset.UtcNow;
        ProcessingLeaseUntil = null;
        NextRecoveryAt = null;
        AddDomainEvent(new PaymentSessionFailedDomainEvent(
            Id, UserId, WalletId, AmountMinor, Currency, Provider,
            ProviderResultDescription, CompletedAt.Value));
    }

    public void MarkAsExpired()
    {
        if (IsTerminal() || Status == PaymentSessionStatus.PendingRecovery)
            return;
        Status = PaymentSessionStatus.Expired;
        CompletedAt = DateTimeOffset.UtcNow;
        ProcessingLeaseUntil = null;
    }

    public void RequireReversal(string reason, bool supported, DateTimeOffset nextAttemptAt)
    {
        ReversalStatus = supported ? FundingReversalStatus.Required : FundingReversalStatus.Unsupported;
        ReversalDescription = Truncate(reason);
        NextReversalAt = supported ? nextAttemptAt : null;
    }

    public void RecordReversalResult(bool succeeded, string? description, DateTimeOffset? retryAt)
    {
        ReversalAttemptCount++;
        ReversalStatus = succeeded ? FundingReversalStatus.Succeeded : FundingReversalStatus.Failed;
        ReversalDescription = Truncate(description);
        NextReversalAt = succeeded ? null : retryAt;
    }

    public void AdvanceVersion() => Version++;
    public bool IsExpired() => DateTimeOffset.UtcNow > ExpiresAt && !IsTerminal();
    public bool IsTerminal() => Status is PaymentSessionStatus.Succeeded
        or PaymentSessionStatus.Failed or PaymentSessionStatus.Expired;
    public string BuildSuccessRedirectUrl() =>
        SucceededCallbackUrl ?? $"{ReturnBaseUrl.TrimEnd('/')}/{Id}";
    public string BuildFailureRedirectUrl() =>
        FailedCallbackUrl ?? $"{ReturnBaseUrl.TrimEnd('/')}/{Id}";
    public bool HasEquivalentInitiation(string hash, short version) =>
        InitiationRequestHashVersion == version &&
        string.Equals(InitiationRequestHash, hash, StringComparison.Ordinal);

    private void EnsureNotTerminal()
    {
        if (IsTerminal())
            throw new InvalidPaymentSessionStateException(
                $"جلسه پرداخت در وضعیت نهایی {Status} قرار دارد و قابل تغییر نیست.");
    }

    private static string? Truncate(string? value) =>
        string.IsNullOrEmpty(value) || value.Length <= DescriptionMaxLength
            ? value : value[..DescriptionMaxLength];
}
