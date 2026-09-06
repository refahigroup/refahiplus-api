using MediatR;
using Refahi.Modules.Commerce.Domain;
using Refahi.Modules.Orders.Application.Contracts.IntegrationEvents;

namespace Refahi.Modules.Commerce.Application.Features.Checkout;

public sealed class CommerceOrderPaidNotificationHandler(ICommerceRepository repository, Refahi.Modules.Commerce.Application.Contracts.Providers.ICommerceMutationLock gate) : INotificationHandler<OrderPaidIntegrationEvent>
{
    public async Task Handle(OrderPaidIntegrationEvent e, CancellationToken ct)
    {
        if (!e.SourceModule.Equals("Commerce", StringComparison.OrdinalIgnoreCase) || !e.SourceReferenceId.HasValue)
            return;

        await using var held = await gate.AcquireAsync(e.SourceReferenceId.Value, ct);
        var value = await repository.GetFreshOrderAsync(e.SourceReferenceId.Value, ct);

        if (value is null || value.OrderId != e.OrderId || value.UserId != e.UserId)
            return;

        value.QueueFulfillment(e.PaymentId); await repository.SaveChangesAsync(ct);
    }
}
