using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Microsoft.Extensions.Logging;
using Refahi.Modules.Wallets.Application.Contracts;
using Refahi.Modules.Wallets.Application.Contracts.Exceptions;
using Refahi.Modules.Wallets.Application.Contracts.Queries;
using Refahi.Modules.Wallets.Application.Contracts.Repositories;
using Refahi.Modules.Wallets.Application.Contracts.Responses;

namespace Refahi.Modules.Wallets.Application.Handlers;

/// <summary>
/// Query handler for retrieving refund details (read-only).
/// </summary>
public sealed class GetRefundQueryHandler 
    : IRequestHandler<GetRefundQuery, CommandResponse<GetRefundResponse>>
{
    private readonly IPaymentReadRepository _repository;
    private readonly ILogger<GetRefundQueryHandler> _logger;

    public GetRefundQueryHandler(
        IPaymentReadRepository repository,
        ILogger<GetRefundQueryHandler> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task<CommandResponse<GetRefundResponse>> Handle(
        GetRefundQuery request,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Querying refund: PaymentId={PaymentId}, RefundId={RefundId}",
            request.PaymentId,
            request.RefundId);

        var result = await _repository.GetRefundAsync(
            request.PaymentId, 
            request.RefundId, 
            cancellationToken);

        if (result == null)
        {
            _logger.LogWarning(
                "Refund not found: PaymentId={PaymentId}, RefundId={RefundId}",
                request.PaymentId,
                request.RefundId);
            throw new RefundNotFoundException(request.RefundId);
        }

        _logger.LogInformation(
            "Refund retrieved: {RefundId}, Status: {Status}, OrderId: {OrderId}",
            result.RefundId,
            result.Status,
            result.OrderId);

        return new CommandResponse<GetRefundResponse>(CommandStatus.Completed, result);
    }
}
