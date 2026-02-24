using System;
using MediatR;
using Refahi.Modules.Wallets.Application.Contracts;
using Refahi.Modules.Wallets.Application.Contracts.Responses;

namespace Refahi.Modules.Wallets.Application.Contracts.Queries;

/// <summary>
/// Query to retrieve payment details (read-only).
/// </summary>
public record GetPaymentQuery(
    Guid PaymentId
) : IRequest<CommandResponse<GetPaymentResponse>>;
