using MediatR;
using Refahi.Modules.Orders.Application.Contracts.Queries;
using Refahi.Modules.Orders.Application.Contracts.Repositories;

namespace Refahi.Modules.Orders.Application.Features.GetStoreVariantSoldQuantity;

public sealed class GetStoreVariantSoldQuantityQueryHandler
    : IRequestHandler<GetStoreVariantSoldQuantityQuery, int>
{
    private readonly IOrderQueryService _orderQueryService;

    public GetStoreVariantSoldQuantityQueryHandler(IOrderQueryService orderQueryService)
    {
        _orderQueryService = orderQueryService;
    }

    public Task<int> Handle(GetStoreVariantSoldQuantityQuery request, CancellationToken cancellationToken)
        => _orderQueryService.GetStoreVariantSoldQuantityAsync(
            request.VariantId,
            request.UsageDate,
            request.CapacityScope,
            request.ExcludeOrderId,
            cancellationToken);
}
