using MediatR;
using Refahi.Modules.Orders.Application.Contracts.Dtos;
using Refahi.Modules.Orders.Application.Contracts.Queries;
using Refahi.Modules.Orders.Domain.Repositories;

namespace Refahi.Modules.Orders.Application.Features.GetOrdersBySource;

public class GetOrdersBySourceQueryHandler : IRequestHandler<GetOrdersBySourceQuery, PaginatedOrdersResponse>
{
    private readonly IOrderRepository _orderRepository;

    public GetOrdersBySourceQueryHandler(IOrderRepository orderRepository)
    {
        _orderRepository = orderRepository;
    }

    public async Task<PaginatedOrdersResponse> Handle(GetOrdersBySourceQuery request, CancellationToken cancellationToken)
    {
        var orders = await _orderRepository.GetBySourceAsync(
            request.SourceModule, request.SourceReferenceId,
            request.PageNumber, request.PageSize,
            cancellationToken);

        var total = await _orderRepository.CountBySourceAsync(
            request.SourceModule, request.SourceReferenceId,
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
