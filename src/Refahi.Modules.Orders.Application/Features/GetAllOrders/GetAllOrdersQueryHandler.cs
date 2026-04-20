using MediatR;
using Refahi.Modules.Orders.Application.Contracts.Dtos;
using Refahi.Modules.Orders.Application.Contracts.Queries;
using Refahi.Modules.Orders.Domain.Repositories;

namespace Refahi.Modules.Orders.Application.Features.GetAllOrders;

public class GetAllOrdersQueryHandler : IRequestHandler<GetAllOrdersQuery, PaginatedOrdersResponse>
{
    private readonly IOrderRepository _orderRepository;

    public GetAllOrdersQueryHandler(IOrderRepository orderRepository)
    {
        _orderRepository = orderRepository;
    }

    public async Task<PaginatedOrdersResponse> Handle(GetAllOrdersQuery request, CancellationToken cancellationToken)
    {
        var orders = await _orderRepository.GetAllAsync(
            request.PageNumber, request.PageSize,
            request.Status, request.UserId, request.SourceModule,
            cancellationToken);

        var total = await _orderRepository.CountAllAsync(
            request.Status, request.UserId, request.SourceModule,
            cancellationToken);

        var totalPages = (int)Math.Ceiling(total / (double)request.PageSize);

        var summaries = orders.Select(o => new OrderSummaryDto(
            Id: o.Id,
            OrderNumber: o.OrderNumber,
            FinalAmountMinor: o.FinalAmountMinor,
            Status: o.Status.ToString(),
            SourceModule: o.SourceModule,
            ItemCount: o.Items.Count,
            CreatedAt: o.CreatedAt)).ToList();

        return new PaginatedOrdersResponse(summaries, request.PageNumber, request.PageSize, total, totalPages);
    }
}
