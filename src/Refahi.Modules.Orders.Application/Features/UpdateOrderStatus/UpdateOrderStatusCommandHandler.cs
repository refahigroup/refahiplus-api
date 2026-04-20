using MediatR;
using Refahi.Modules.Orders.Application.Contracts.Commands;
using Refahi.Modules.Orders.Application.Contracts.IntegrationEvents;
using Refahi.Modules.Orders.Domain.Enums;
using Refahi.Modules.Orders.Domain.Repositories;

namespace Refahi.Modules.Orders.Application.Features.UpdateOrderStatus;

public class UpdateOrderStatusCommandHandler : IRequestHandler<UpdateOrderStatusCommand, UpdateOrderStatusResponse>
{
    private readonly IOrderRepository _orderRepository;
    private readonly IPublisher _publisher;

    public UpdateOrderStatusCommandHandler(IOrderRepository orderRepository, IPublisher publisher)
    {
        _orderRepository = orderRepository;
        _publisher = publisher;
    }

    public async Task<UpdateOrderStatusResponse> Handle(UpdateOrderStatusCommand request, CancellationToken cancellationToken)
    {
        var order = await _orderRepository.GetByIdAsync(request.OrderId, cancellationToken)
            ?? throw new InvalidOperationException("سفارش یافت نشد");

        var newStatus = (OrderStatus)(short)request.NewStatus;
        order.UpdateStatus(newStatus);

        await _orderRepository.UpdateAsync(order, cancellationToken);
        // Domain events are now captured to Outbox by SaveChangesAsync override

        // Integration event: cross-module notification for Delivered status
        if (newStatus == OrderStatus.Delivered)
        {
            await _publisher.Publish(new OrderDeliveredIntegrationEvent(
                EventId: Guid.NewGuid(),
                OrderId: order.Id,
                OrderNumber: order.OrderNumber,
                UserId: order.UserId,
                SourceModule: order.SourceModule,
                SourceReferenceId: order.SourceReferenceId,
                OccurredAt: DateTimeOffset.UtcNow), cancellationToken);
        }

        return new UpdateOrderStatusResponse(order.Id, order.Status.ToString());
    }
}
