using MediatR;
using Refahi.Modules.PaymentGateway.Domain.Enums;
using System;

namespace Refahi.Modules.PaymentGateway.Application.Contracts.Features.InitiatePayment;

/// <summary>
/// Initiates a new payment gateway session for wallet top-up.
/// Creates a session, requests a token from the provider, and returns
/// the redirect URL to send the user to the payment page.
/// </summary>
public sealed record InitiatePaymentCommand(
    Guid UserId,
    Guid WalletId,
    long AmountMinor,
    string Currency,
    PaymentGatewayProviderType Provider,
    /// <summary>
    /// Absolute URL where the provider should POST the payment result.
    /// Built by the API layer from the current request context.
    /// e.g. "https://api.refahi.xyz/api/payment-gateway/callback/sep"
    /// </summary>
    string ProviderCallbackUrl,
    /// <summary>
    /// Base URL of the Blazor result page.
    /// Backend appends /{sessionId} → e.g. "/charge/wallet/topup/result/{sessionId}"
    /// </summary>
    string ReturnBaseUrl,
    string? SucceededCallbackUrl = null,
    string? FailedCallbackUrl = null
) : IRequest<InitiatePaymentResponse>;
