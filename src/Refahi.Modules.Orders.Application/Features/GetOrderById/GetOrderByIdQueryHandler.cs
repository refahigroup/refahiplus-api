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

        // Ownership check: User role can only see their own orders
        // Return null (404) instead of Forbidden to prevent GUID enumeration
        if (request.CallerRole == "User" && order.UserId != request.CallerUserId)
            return null;

        var items = order.Items.Select(i => new OrderItemDto(
            Id: i.Id,
            Title: i.Title,
            UnitPriceMinor: i.UnitPriceMinor,
            Quantity: i.Quantity,
            FinalPriceMinor: i.FinalPriceMinor,
            CategoryCode: i.CategoryCode,
            Tags: i.Tags,
            MetadataJson: i.MetadataJson,
            DeliveryMethod: (short)i.DeliveryMethod)).ToList();

        return new OrderDto(
            Id: order.Id,
            OrderNumber: order.OrderNumber,
            UserId: order.UserId,
            TotalAmountMinor: order.TotalAmountMinor,
            DiscountAmountMinor: order.DiscountAmountMinor,
            ShippingFeeMinor: order.ShippingFeeMinor,
            DiscountCode: order.DiscountCode,
            DiscountCodeAmountMinor: order.DiscountCodeAmountMinor,
            FinalAmountMinor: order.FinalAmountMinor,
            Status: order.Status.ToString(),
            PaymentState: order.PaymentState.ToString(),
            SourceModule: order.SourceModule,
            SourceReferenceId: order.SourceReferenceId,
            ShippingAddressId: order.ShippingAddressId,
            ShippingAddressSnapshotJson: order.ShippingAddressSnapshotJson,
            DeliveryDate: order.DeliveryDate,
            DeliveryTimeSlot: (short)order.DeliveryTimeSlot,
            Items: items,
            CreatedAt: order.CreatedAt);
    }
}
