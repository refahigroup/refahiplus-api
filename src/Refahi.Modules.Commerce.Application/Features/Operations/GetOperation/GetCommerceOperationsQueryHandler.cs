using MediatR;
using Refahi.Modules.Commerce.Application.Contracts;
using Refahi.Modules.Commerce.Domain;

namespace Refahi.Modules.Commerce.Application.Features.Operations.GetOperation;

public sealed class GetCommerceOperationsQueryHandler(ICommerceRepository repository, ICommerceSecretProtector secrets) :
    IRequestHandler<GetCommerceOperationsQuery, IReadOnlyList<CommerceOperationDto>>
{
    public async Task<IReadOnlyList<CommerceOperationDto>> Handle(GetCommerceOperationsQuery request, CancellationToken ct)
    {
        var orders = await repository.GetOperationsAsync(
            request.Status,
            (Math.Max(1, request.PageNumber) - 1) * Math.Clamp(request.PageSize, 1, 100),
            Math.Clamp(request.PageSize, 1, 100)
        , ct);

        return orders.SelectMany(o => o.Fulfillments.Select(f => new CommerceOperationDto(
            o.Id,
            f.Id,
            f.ProviderKey,
            f.Status.ToString(),
            f.FailureReason,
            o.UpdatedAt,
            f.Attempts.OrderByDescending(a => a.StartedAt)
                      .Select(a => new CommerceOperationAttemptDto(
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
