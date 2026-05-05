using MediatR;
using Refahi.Modules.Orders.Application.Contracts.Repositories;
using Refahi.Modules.Orders.Application.Contracts.Dtos;
using Refahi.Modules.Orders.Application.Contracts.Queries;

namespace Refahi.Modules.Orders.Application.Features.GetUserOrders;

public class GetUserOrdersQueryHandler : IRequestHandler<GetUserOrdersQuery, PaginatedOrdersResponse>
{
    private readonly IOrderQueryService _orderQueryService;

    public GetUserOrdersQueryHandler(IOrderQueryService orderQueryService)
    {
        _orderQueryService = orderQueryService;
    }

    public async Task<PaginatedOrdersResponse> Handle(GetUserOrdersQuery request, CancellationToken cancellationToken)
    {
        var summaries = await _orderQueryService.GetUserOrderSummariesAsync(
            request.UserId, request.Statuses, request.SourceModule, request.PageNumber, request.PageSize, cancellationToken);

        var total = await _orderQueryService.CountUserOrdersAsync(
            request.UserId, request.Statuses, request.SourceModule, cancellationToken);

        var totalPages = (int)Math.Ceiling(total / (double)request.PageSize);

        return new PaginatedOrdersResponse(summaries, request.PageNumber, request.PageSize, total, totalPages);
    }
}


