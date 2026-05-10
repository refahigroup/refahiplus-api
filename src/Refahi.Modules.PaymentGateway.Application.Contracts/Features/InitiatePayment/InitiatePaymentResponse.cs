using System;

namespace Refahi.Modules.PaymentGateway.Application.Contracts.Features.InitiatePayment;

public sealed record InitiatePaymentResponse(
    Guid SessionId,
    /// <summary>The full URL to redirect the user's browser to the payment page.</summary>
    string GatewayRedirectUrl
);
