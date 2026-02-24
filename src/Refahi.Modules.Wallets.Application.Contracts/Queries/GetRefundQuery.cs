using System;
using MediatR;
using Refahi.Modules.Wallets.Application.Contracts;
using Refahi.Modules.Wallets.Application.Contracts.Responses;

namespace Refahi.Modules.Wallets.Application.Contracts.Queries;

/// <summary>
/// Query to retrieve refund details (read-only).
/// </summary>
public record GetRefundQuery(
    Guid PaymentId,
    Guid RefundId
) : IRequest<CommandResponse<GetRefundResponse>>;
