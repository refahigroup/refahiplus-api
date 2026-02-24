using MediatR;
using Microsoft.Extensions.Logging;
using Refahi.Modules.Wallets.Application.Contracts;
using Refahi.Modules.Wallets.Application.Contracts.Exceptions;
using Refahi.Modules.Wallets.Application.Contracts.Queries;
using Refahi.Modules.Wallets.Application.Contracts.Repositories;
using Refahi.Modules.Wallets.Application.Contracts.Responses;
using System.Threading;
using System.Threading.Tasks;

namespace Refahi.Modules.Wallets.Application.Handlers;

/// <summary>
/// Query handler for retrieving payment details (read-only).
/// </summary>
public sealed class GetPaymentQueryHandler 
    : IRequestHandler<GetPaymentQuery, CommandResponse<GetPaymentResponse>>
{
    private readonly IPaymentReadRepository _repository;
    private readonly ILogger<GetPaymentQueryHandler> _logger;

    public GetPaymentQueryHandler(
        IPaymentReadRepository repository,
        ILogger<GetPaymentQueryHandler> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task<CommandResponse<GetPaymentResponse>> Handle(
        GetPaymentQuery request,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Querying payment: {PaymentId}",
            request.PaymentId);

        var result = await _repository.GetPaymentAsync(request.PaymentId, cancellationToken);

        if (result == null)
        {
            _logger.LogWarning(
                "Payment not found: {PaymentId}",
                request.PaymentId);
            throw new PaymentNotFoundException(request.PaymentId);
        }

        _logger.LogInformation(
            "Payment retrieved: {PaymentId}, Status: {Status}, OrderId: {OrderId}",
            result.PaymentId,
            result.Status,
            result.OrderId);

        return new CommandResponse<GetPaymentResponse>(CommandStatus.Completed, result);
    }
}
