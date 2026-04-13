using MediatR;
using Refahi.Modules.Orders.Application.Contracts.Dtos;
using Refahi.Modules.Orders.Application.Contracts.Queries;
using Refahi.Modules.Orders.Domain.Repositories;

namespace Refahi.Modules.Orders.Application.Features.GetOrderById;

public class GetOrderByIdQueryHandler : IRequestHandler<GetOrderByIdQuery, OrderDto?>
{
    private readonly IOrderRepository _orderRepository;

    public GetOrderByIdQueryHandler(IOrderRepository orderRepository)
    {
        _orderRepository = orderRepository;
    }

    public async Task<OrderDto?> Handle(GetOrderByIdQuery request, CancellationToken cancellationToken)
    {
        var order = await _orderRepository.GetByIdWithItemsAsync(request.OrderId, cancellationToken);
        if (order is null) return null;

        var items = order.Items.Select(i => new OrderItemDto(
            Id: i.Id,
            Title: i.Title,
            UnitPriceMinor: i.UnitPriceMinor,
            Quantity: i.Quantity,
            FinalPriceMinor: i.FinalPriceMinor,
            CategoryCode: i.CategoryCode,
            Tags: i.Tags,
            MetadataJson: i.MetadataJson)).ToList();

        return new OrderDto(
            Id: order.Id,
            OrderNumber: order.OrderNumber,
            UserId: order.UserId,
            TotalAmountMinor: order.TotalAmountMinor,
            DiscountAmountMinor: order.DiscountAmountMinor,
            FinalAmountMinor: order.FinalAmountMinor,
            Status: order.Status.ToString(),
            PaymentState: order.PaymentState.ToString(),
            SourceModule: order.SourceModule,
            SourceReferenceId: order.SourceReferenceId,
            Items: items,
            CreatedAt: order.CreatedAt);
    }
}
