using MediatR;
using Refahi.Modules.Commerce.Domain;
using Refahi.Modules.Orders.Application.Contracts.IntegrationEvents;

namespace Refahi.Modules.Commerce.Application.Features.Checkout;

public sealed class CommerceOrderCancelledNotificationtHandler(ICommerceRepository repository) :
    INotificationHandler<OrderCancelledIntegrationEvent>, INotificationHandler<OrderRefundedIntegrationEvent>
{
    public async Task Handle(OrderCancelledIntegrationEvent e, CancellationToken ct)
    {
        if (!e.SourceModule.Equals("Commerce", StringComparison.OrdinalIgnoreCase))
            return;

        var value = await repository.GetOrderByOrderIdAsync(e.OrderId, ct);

        if (value is null)
            return;

        value.MarkCancelled(e.PaymentAction == "Refunded");

        await repository.SaveChangesAsync(ct);
    }

    public async Task Handle(OrderRefundedIntegrationEvent e, CancellationToken ct)
    {
        var value = await repository.GetOrderByOrderIdAsync(e.OrderId, ct);

        if (value is null)
            return;

        value.MarkCancelled(true);

        await repository.SaveChangesAsync(ct);
    }
}
