using MediatR;
using System;

namespace Refahi.Modules.PaymentGateway.Application.Contracts.Features.GetPaymentSession;

public sealed record GetPaymentSessionQuery(
    Guid SessionId,
    /// <summary>Used to enforce ownership — only the session owner can query their session.</summary>
    Guid RequestingUserId
) : IRequest<PaymentSessionDto?>;
