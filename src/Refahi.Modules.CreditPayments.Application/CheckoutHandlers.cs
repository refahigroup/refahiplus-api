using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Refahi.Modules.CreditPayments.Application.Contracts;
using Refahi.Modules.CreditPayments.Domain;
using Refahi.Modules.Identity.Application.Contracts.Queries;
using Refahi.Shared.Services.Notification;

namespace Refahi.Modules.CreditPayments.Application;

public sealed class GetCheckoutHandler(
    ICheckoutRepository checkouts,
    ICheckoutSecurityService security,
    TimeProvider clock) : IRequestHandler<GetCheckoutQuery, CheckoutDto?>
{
    public async Task<CheckoutDto?> Handle(GetCheckoutQuery request, CancellationToken ct)
    {
        if (!security.TryHashPresentedToken(request.Token, out var hash)) return null;
        var lookup = await checkouts.FindCheckoutAsync(hash, false, ct);
        if (lookup is null) return null;
        EnsureUsable(lookup, clock.GetUtcNow());
        return new(
            CreateGatewayPaymentHandler.PaymentStatusName(lookup.Payment.Status),
            lookup.Payment.MerchantDisplayNameSnapshot,
            lookup.Payment.TerminalNameSnapshot,
            lookup.Payment.AmountMinor,
            lookup.Payment.Currency,
            lookup.Payment.Description,
            lookup.Session.ExpiresAt,
            lookup.Session.Status is CheckoutSessionStatus.Authenticated or CheckoutSessionStatus.AwaitingFunding
                or CheckoutSessionStatus.Processing or CheckoutSessionStatus.Completed
                ? "authenticated"
                : "authentication_required",
            lookup.Payment.PaidAt,
            lookup.Payment.Status == GatewayPaymentStatus.Succeeded ? lookup.Payment.ReturnUrl : null);
    }

    internal static void EnsureUsable(CheckoutLookup lookup, DateTimeOffset now)
    {
        if (now >= lookup.Session.ExpiresAt || now >= lookup.Payment.ExpiresAt)
            throw Error("checkout_expired", "نشست پرداخت منقضی شده است", 410);
        if (lookup.Session.Status is not (CheckoutSessionStatus.Created or CheckoutSessionStatus.Authenticated
                or CheckoutSessionStatus.AwaitingFunding or CheckoutSessionStatus.Processing or CheckoutSessionStatus.Completed
                or CheckoutSessionStatus.Canceled)
            || lookup.Payment.Status is not (GatewayPaymentStatus.AwaitingCustomer or GatewayPaymentStatus.AwaitingFunding
                or GatewayPaymentStatus.Processing or GatewayPaymentStatus.Succeeded or GatewayPaymentStatus.Canceled))
            throw Error("checkout_not_found", "نشست پرداخت یافت نشد", 404);
    }

    internal static CreditPaymentsApplicationException Error(string code, string message, int status = 400) =>
        new(code, message, status);
}

public sealed class SendCheckoutOtpHandler(
    ICheckoutRepository checkouts,
    ICheckoutSecurityService security,
    ICheckoutOtpReferenceProtector references,
    INotificationService notification,
    IMediator mediator,
    IOptions<CheckoutOptions> options,
    TimeProvider clock,
    ILogger<SendCheckoutOtpHandler> logger)
    : IRequestHandler<SendCheckoutOtpCommand, CheckoutOtpChallengeDto>
{
    private const string GenericMessage = "در صورت معتبر بودن اطلاعات، کد تأیید ارسال شد.";

    public async Task<CheckoutOtpChallengeDto> Handle(SendCheckoutOtpCommand request, CancellationToken ct)
    {
        if (!security.TryHashPresentedToken(request.Token, out var tokenHash))
            throw GetCheckoutHandler.Error("checkout_not_found", "نشست پرداخت یافت نشد", 404);

        var identity = await mediator.Send(new ResolveAuthenticationIdentityQuery(request.Identifier), ct);
        var trustedIdentity = identity is { CanAuthenticate: true, OtpReceipt: not null, OtpReceiptType: not null }
            ? identity
            : null;
        var now = clock.GetUtcNow();
        var configured = options.Value;
        var provisionalExpiry = now.AddSeconds(Math.Max(1, configured.SyntheticChallengeExpirySeconds));
        OtpChallengeDispatch dispatch;
        try
        {
            dispatch = await checkouts.ReserveOtpChallengeAsync(
                tokenHash,
                trustedIdentity?.UserId,
                (short)OtpType.SignIn,
                now,
                provisionalExpiry,
                TimeSpan.FromSeconds(Math.Max(1, configured.OtpResendCooldownSeconds)),
                Math.Max(1, configured.MaximumOtpSends),
                ct);
        }
        catch (CheckoutStateException ex)
        {
            throw MapState(ex);
        }

        if (trustedIdentity is null)
        {
            await checkouts.FinalizeOtpChallengeAsync(
                dispatch.Challenge.Id, null, dispatch.Challenge.ExpiresAt, true, now, ct);
            logger.LogInformation("Synthetic checkout OTP challenge created. CheckoutSessionId={CheckoutSessionId} OtpChallengeId={OtpChallengeId}",
                dispatch.Session.Id, dispatch.Challenge.Id);
            return ToDto(dispatch.Challenge, configured, GenericMessage);
        }

        try
        {
            var sent = await notification.SendOtp(
                trustedIdentity.OtpReceipt!, trustedIdentity.OtpReceiptType!.Value, OtpType.SignIn, ct);
            var providerExpiry = new DateTimeOffset(sent.ExpiresAt.ToUniversalTime());
            var effectiveExpiry = providerExpiry <= dispatch.Session.ExpiresAt
                ? providerExpiry
                : dispatch.Session.ExpiresAt;
            if (effectiveExpiry <= now)
                throw new InvalidOperationException("OTP provider returned an expired challenge.");
            var protectedReference = references.Protect(sent.ReferenceCode);
            await checkouts.FinalizeOtpChallengeAsync(
                dispatch.Challenge.Id, protectedReference, effectiveExpiry, false, now, ct);
            logger.LogInformation("Checkout OTP requested. CheckoutSessionId={CheckoutSessionId} OtpChallengeId={OtpChallengeId}",
                dispatch.Session.Id, dispatch.Challenge.Id);
            return new(dispatch.Challenge.Id, effectiveExpiry,
                Min(dispatch.Challenge.SentAt.AddSeconds(configured.OtpResendCooldownSeconds), dispatch.Session.ExpiresAt),
                GenericMessage);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            await checkouts.MarkOtpDispatchFailedAsync(dispatch.Challenge.Id, clock.GetUtcNow(), ct);
            logger.LogWarning(ex, "Checkout OTP provider unavailable. CheckoutSessionId={CheckoutSessionId} OtpChallengeId={OtpChallengeId}",
                dispatch.Session.Id, dispatch.Challenge.Id);
            throw GetCheckoutHandler.Error("temporarily_unavailable", "سرویس ارسال کد تأیید موقتاً در دسترس نیست", 503);
        }
    }

    private static CheckoutOtpChallengeDto ToDto(
        CheckoutOtpChallenge challenge,
        CheckoutOptions options,
        string message) => new(
            challenge.Id,
            challenge.ExpiresAt,
            Min(challenge.SentAt.AddSeconds(options.OtpResendCooldownSeconds), challenge.ExpiresAt),
            message);

    private static DateTimeOffset Min(DateTimeOffset left, DateTimeOffset right) => left <= right ? left : right;

    private static CreditPaymentsApplicationException MapState(CheckoutStateException ex) => ex.Code switch
    {
        "otp_rate_limited" => GetCheckoutHandler.Error(ex.Code, ex.Message, 429),
        "checkout_expired" => GetCheckoutHandler.Error(ex.Code, ex.Message, 410),
        "checkout_not_found" => GetCheckoutHandler.Error(ex.Code, ex.Message, 404),
        "checkout_already_authenticated" => GetCheckoutHandler.Error(ex.Code, ex.Message, 409),
        _ => GetCheckoutHandler.Error(ex.Code, ex.Message)
    };
}

public sealed class VerifyCheckoutOtpHandler(
    ICheckoutRepository checkouts,
    ICheckoutSecurityService security,
    ICheckoutOtpReferenceProtector references,
    INotificationService notification,
    IMediator mediator,
    IOptions<CheckoutOptions> options,
    TimeProvider clock,
    ILogger<VerifyCheckoutOtpHandler> logger)
    : IRequestHandler<VerifyCheckoutOtpCommand, CheckoutOtpVerificationResult>
{
    public async Task<CheckoutOtpVerificationResult> Handle(VerifyCheckoutOtpCommand request, CancellationToken ct)
    {
        if (!security.TryHashPresentedToken(request.Token, out var tokenHash))
            throw GetCheckoutHandler.Error("checkout_not_found", "نشست پرداخت یافت نشد", 404);
        var now = clock.GetUtcNow();
        var existing = await checkouts.FindCheckoutAsync(tokenHash, false, ct);
        if (existing is null)
            throw GetCheckoutHandler.Error("checkout_not_found", "نشست پرداخت یافت نشد", 404);
        if (existing.Session.Status == CheckoutSessionStatus.Authenticated)
        {
            var authorized = existing.Session.AuthorizationSecretHash is not null
                && existing.Session.AuthorizationExpiresAt > now
                && !string.IsNullOrWhiteSpace(request.ExistingAuthorizationSecret)
                && security.VerifyAuthorizationSecret(
                    request.ExistingAuthorizationSecret, existing.Session.AuthorizationSecretHash);
            if (!authorized)
                throw GetCheckoutHandler.Error("checkout_already_authenticated", "نشست پرداخت قبلاً احراز هویت شده است", 409);
            return new("authenticated", null, existing.Session.AuthorizationExpiresAt, true);
        }
        GetCheckoutHandler.EnsureUsable(existing, now);

        OtpChallengeDispatch? dispatch;
        try
        {
            dispatch = await checkouts.BeginOtpVerificationAsync(
                tokenHash, request.ChallengeId, now, Math.Max(1, options.Value.MaximumOtpVerifyAttempts), ct);
        }
        catch (CheckoutStateException ex)
        {
            throw ex.Code == "checkout_expired"
                ? GetCheckoutHandler.Error(ex.Code, ex.Message, 410)
                : GetCheckoutHandler.Error("otp_invalid_or_expired", "کد تأیید نامعتبر یا منقضی شده است");
        }
        if (dispatch is null || dispatch.Challenge.Status == CheckoutOtpChallengeStatus.Synthetic
            || dispatch.Challenge.ResolvedCustomerId is null
            || string.IsNullOrWhiteSpace(dispatch.Challenge.ProtectedReferenceCode)
            || !references.TryUnprotect(dispatch.Challenge.ProtectedReferenceCode, out var providerReference))
        {
            if (dispatch is not null)
                await RecordFailure(dispatch.Challenge.Id, "synthetic_or_invalid", ct);
            throw InvalidOtp();
        }

        OtpValidationResult validation;
        try
        {
            validation = await notification.ValidateOtp(providerReference, request.Otp, OtpType.SignIn, ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Checkout OTP validation provider unavailable. CheckoutSessionId={CheckoutSessionId} OtpChallengeId={OtpChallengeId}",
                dispatch.Session.Id, dispatch.Challenge.Id);
            throw GetCheckoutHandler.Error("temporarily_unavailable", "سرویس تأیید کد موقتاً در دسترس نیست", 503);
        }
        if (!validation.IsValid)
        {
            await RecordFailure(dispatch.Challenge.Id, "invalid", ct);
            throw InvalidOtp();
        }

        var customerId = dispatch.Challenge.ResolvedCustomerId.Value;
        var eligibility = await mediator.Send(new GetUserAuthenticationEligibilityQuery(customerId), ct);
        if (!eligibility.CanAuthenticate)
        {
            await RecordFailure(dispatch.Challenge.Id, "identity_ineligible", ct);
            throw InvalidOtp();
        }

        var authorization = security.CreateAuthorizationSecret();
        try
        {
            await checkouts.AuthenticateCheckoutAsync(tokenHash, dispatch.Challenge.Id, customerId,
                authorization.SecretHash, dispatch.Session.ExpiresAt, now, ct);
        }
        catch (CheckoutStateException ex)
        {
            throw GetCheckoutHandler.Error(ex.Code, ex.Message, ex.Code == "customer_switch_forbidden" ? 409 : 400);
        }
        logger.LogInformation("Checkout customer authenticated. CheckoutSessionId={CheckoutSessionId} OtpChallengeId={OtpChallengeId} CustomerId={CustomerId}",
            dispatch.Session.Id, dispatch.Challenge.Id, customerId);
        return new("authenticated", authorization.Secret, dispatch.Session.ExpiresAt, false);
    }

    private Task RecordFailure(Guid challengeId, string category, CancellationToken ct) =>
        checkouts.RecordOtpValidationFailureAsync(
            challengeId, Math.Max(1, options.Value.MaximumOtpVerifyAttempts), clock.GetUtcNow(), category, ct);

    private static CreditPaymentsApplicationException InvalidOtp() =>
        GetCheckoutHandler.Error("otp_invalid_or_expired", "کد تأیید نامعتبر یا منقضی شده است");
}
