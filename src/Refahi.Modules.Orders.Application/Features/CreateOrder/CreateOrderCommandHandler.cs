using MediatR;
using Refahi.Modules.Orders.Application.Contracts.Commands;
using Refahi.Modules.Orders.Domain.Aggregates;
using Refahi.Modules.Orders.Domain.Enums;
using Refahi.Modules.Orders.Domain.Repositories;

namespace Refahi.Modules.Orders.Application.Features.CreateOrder;

public class CreateOrderCommandHandler : IRequestHandler<CreateOrderCommand, CreateOrderResponse>
{
    private readonly IOrderRepository _orderRepository;

    public CreateOrderCommandHandler(IOrderRepository orderRepository)
    {
        _orderRepository = orderRepository;
    }

    public async Task<CreateOrderResponse> Handle(CreateOrderCommand request, CancellationToken cancellationToken)
    {
        // Idempotency check
        var existing = await _orderRepository.GetByIdempotencyKeyAsync(request.IdempotencyKey, cancellationToken);
        if (existing is not null)
            return new CreateOrderResponse(existing.Id, existing.OrderNumber, existing.FinalAmountMinor);

        var items = request.Items.Select(i => new OrderItemData(
            Title: i.Title,
            UnitPriceMinor: i.UnitPriceMinor,
            Quantity: i.Quantity,
            DiscountAmountMinor: i.DiscountAmountMinor,
            SourceItemId: i.SourceItemId,
            CategoryCode: i.CategoryCode,
            Tags: i.Tags,
            MetadataJson: i.MetadataJson,
            DeliveryMethod: (DeliveryMethod)i.DeliveryMethod)).ToList();

        var order = Order.Create(
            userId: request.UserId,
            sourceModule: request.SourceModule,
            sourceReferenceId: request.SourceReferenceId,
            idempotencyKey: request.IdempotencyKey,
            items: items,
            shippingAddressId: request.ShippingAddressId,
            shippingAddressSnapshotJson: request.ShippingAddressSnapshotJson,
            deliveryDate: request.DeliveryDate,
            deliveryTimeSlot: (DeliveryTimeSlot)request.DeliveryTimeSlot,
            shippingFeeMinor: request.ShippingFeeMinor,
            discountCode: request.DiscountCode,
            discountCodeAmountMinor: request.DiscountCodeAmountMinor);

        await _orderRepository.AddAsync(order, cancellationToken);
        // Domain events are captured to Outbox by SaveChangesAsync override in OrdersDbContext

        return new CreateOrderResponse(order.Id, order.OrderNumber, order.FinalAmountMinor);
    }
}
