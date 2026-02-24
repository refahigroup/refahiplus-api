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
/// Query handler for retrieving payment intent details (read-only).
/// </summary>
public sealed class GetPaymentIntentQueryHandler 
    : IRequestHandler<GetPaymentIntentQuery, CommandResponse<GetPaymentIntentResponse>>
{
    private readonly IPaymentReadRepository _repository;
    private readonly ILogger<GetPaymentIntentQueryHandler> _logger;

    public GetPaymentIntentQueryHandler(
        IPaymentReadRepository repository,
        ILogger<GetPaymentIntentQueryHandler> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task<CommandResponse<GetPaymentIntentResponse>> Handle(
        GetPaymentIntentQuery request,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Querying payment intent: {IntentId}",
            request.IntentId);

        var result = await _repository.GetPaymentIntentAsync(request.IntentId, cancellationToken);

        if (result == null)
        {
            _logger.LogWarning(
                "Payment intent not found: {IntentId}",
                request.IntentId);
            throw new PaymentIntentNotFoundException(request.IntentId);
        }

        _logger.LogInformation(
            "Payment intent retrieved: {IntentId}, Status: {Status}",
            result.IntentId,
            result.Status);

        return new CommandResponse<GetPaymentIntentResponse>(CommandStatus.Completed, result);
    }
}
