using MediatR;
using Refahi.Modules.Orders.Application.Contracts.Repositories;
using Refahi.Modules.Orders.Application.Contracts.Dtos;
using Refahi.Modules.Orders.Application.Contracts.Queries;
using Refahi.Modules.Orders.Domain.Repositories;

namespace Refahi.Modules.Orders.Application.Features.GetUserOrders;

public class GetUserOrdersQueryHandler : IRequestHandler<GetUserOrdersQuery, PaginatedOrdersResponse>
{
    private readonly IOrderQueryService _orderQueryService;
    private readonly IOrderRepository _orderRepository;

    public GetUserOrdersQueryHandler(IOrderQueryService orderQueryService, IOrderRepository orderRepository)
    {
        _orderQueryService = orderQueryService;
        _orderRepository = orderRepository;
    }

    public async Task<PaginatedOrdersResponse> Handle(GetUserOrdersQuery request, CancellationToken cancellationToken)
    {
        var summaries = await _orderQueryService.GetUserOrderSummariesAsync(
            request.UserId, request.PageNumber, request.PageSize, cancellationToken);

        var total = await _orderRepository.CountByUserIdAsync(request.UserId, cancellationToken);
        var totalPages = (int)Math.Ceiling(total / (double)request.PageSize);

        return new PaginatedOrdersResponse(summaries, request.PageNumber, request.PageSize, total, totalPages);
    }
}

