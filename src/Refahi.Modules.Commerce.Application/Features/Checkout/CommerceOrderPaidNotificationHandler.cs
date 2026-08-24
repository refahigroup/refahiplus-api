using MediatR;
using Refahi.Modules.Commerce.Domain;
using Refahi.Modules.Orders.Application.Contracts.IntegrationEvents;

namespace Refahi.Modules.Commerce.Application.Features.Checkout;

public sealed class CommerceOrderPaidNotificationHandler(ICommerceRepository repository) : INotificationHandler<OrderPaidIntegrationEvent>
{
    public async Task Handle(OrderPaidIntegrationEvent e, CancellationToken ct)
    {
        if (!e.SourceModule.Equals("Commerce", StringComparison.OrdinalIgnoreCase) || !e.SourceReferenceId.HasValue)
            return;

        var value = await repository.GetOrderAsync(e.SourceReferenceId.Value, ct);

        if (value is null || value.OrderId != e.OrderId || value.UserId != e.UserId)
            return;

        value.QueueFulfillment(e.PaymentId); await repository.SaveChangesAsync(ct);
    }
}
