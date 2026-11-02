using MediatR;
using Refahi.Modules.Entertainments.Domain;
using Refahi.Modules.Orders.Application.Contracts.IntegrationEvents;

namespace Refahi.Modules.Entertainments.Application.Features.Checkout;

public sealed class EntertainmentPurchaseCancelledNotificationtHandler(IEntertainmentRepository repository, Refahi.Modules.Entertainments.Application.Contracts.Providers.IEntertainmentMutationLock gate) :
    INotificationHandler<OrderCancelledIntegrationEvent>, INotificationHandler<OrderRefundedIntegrationEvent>
{
    public async Task Handle(OrderCancelledIntegrationEvent e, CancellationToken ct)
    {
        if (!e.SourceModule.Equals("Entertainments", StringComparison.OrdinalIgnoreCase))
            return;

        var value = await repository.GetOrderByOrderIdAsync(e.OrderId, ct);

        if (value is null)
            return;

        await using var held = await gate.AcquireAsync(value.Id, ct);
        value = await repository.GetFreshOrderAsync(value.Id, ct) ?? throw new InvalidOperationException();
        value.MarkCancelled(e.PaymentAction == "Refunded");

        await repository.SaveChangesAsync(ct);
    }

    public async Task Handle(OrderRefundedIntegrationEvent e, CancellationToken ct)
    {
        var value = await repository.GetOrderByOrderIdAsync(e.OrderId, ct);

        if (value is null)
            return;

        await using var held = await gate.AcquireAsync(value.Id, ct);
        value = await repository.GetFreshOrderAsync(value.Id, ct) ?? throw new InvalidOperationException();
        value.MarkCancelled(true);

        await repository.SaveChangesAsync(ct);
    }
}
