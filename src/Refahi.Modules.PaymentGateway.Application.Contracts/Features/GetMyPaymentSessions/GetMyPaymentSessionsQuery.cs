using MediatR;
using Refahi.Modules.PaymentGateway.Application.Contracts.Features.GetPaymentSession;
using Refahi.Modules.PaymentGateway.Domain.Enums;
using System;
using System.Collections.Generic;

namespace Refahi.Modules.PaymentGateway.Application.Contracts.Features.GetMyPaymentSessions;

public sealed record GetMyPaymentSessionsQuery(
    Guid UserId,
    int Take = 20,
    PaymentSessionStatus? Status = null) : IRequest<IReadOnlyList<PaymentSessionDto>>;
