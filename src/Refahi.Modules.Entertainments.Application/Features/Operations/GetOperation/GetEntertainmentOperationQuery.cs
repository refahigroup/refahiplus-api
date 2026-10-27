using MediatR;
using Refahi.Modules.Entertainments.Domain;
namespace Refahi.Modules.Entertainments.Application.Features.Operations.GetOperation;

public sealed record GetEntertainmentOperationQuery(Guid FulfillmentId) : IRequest<EntertainmentOperationDto?>;
public sealed class GetEntertainmentOperationHandler(IEntertainmentRepository repository) : IRequestHandler<GetEntertainmentOperationQuery, EntertainmentOperationDto?>
{
    public async Task<EntertainmentOperationDto?> Handle(GetEntertainmentOperationQuery request, CancellationToken ct)
    {
        var purchase = await repository.GetOrderByFulfillmentIdAsync(request.FulfillmentId, ct);
        var value = purchase?.Fulfillments.SingleOrDefault(x => x.Id == request.FulfillmentId);
        return value is null ? null : new(purchase!.Id, value.Id, value.ProviderKey, value.Status.ToString(), value.FailureReason, purchase.UpdatedAt,
            value.Attempts.Select(x => new EntertainmentOperationAttemptDto(x.Id, x.Operation, x.IdempotencyKey, x.PayloadHash, x.Outcome.ToString(), x.SanitizedError, x.StartedAt, x.CompletedAt)).ToArray());
    }
}
