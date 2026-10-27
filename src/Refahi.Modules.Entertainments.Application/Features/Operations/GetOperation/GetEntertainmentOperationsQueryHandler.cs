using MediatR;
using Refahi.Modules.Entertainments.Domain;

namespace Refahi.Modules.Entertainments.Application.Features.Operations.GetOperation;

public sealed class GetEntertainmentOperationsQueryHandler(IEntertainmentRepository repository) :
    IRequestHandler<GetEntertainmentOperationsQuery, IReadOnlyList<EntertainmentOperationDto>>
{
    public async Task<IReadOnlyList<EntertainmentOperationDto>> Handle(GetEntertainmentOperationsQuery request, CancellationToken ct)
    {
        var orders = await repository.GetOperationsAsync(
            request.Status,
            (Math.Max(1, request.PageNumber) - 1) * Math.Clamp(request.PageSize, 1, 100),
            Math.Clamp(request.PageSize, 1, 100)
        , ct);

        return orders.SelectMany(o => o.Fulfillments.Select(f => new EntertainmentOperationDto(
            o.Id,
            f.Id,
            f.ProviderKey,
            f.Status.ToString(),
            f.FailureReason,
            o.UpdatedAt,
            f.Attempts.OrderByDescending(a => a.StartedAt)
                      .Select(a => new EntertainmentOperationAttemptDto(
                          a.Id,
                          a.Operation,
                          a.IdempotencyKey,
                          a.PayloadHash,
                          a.Outcome.ToString(),
                          a.SanitizedError,
                          a.StartedAt,
                          a.CompletedAt)
                      ).ToArray()
            )
        )).ToArray();
    }
}
