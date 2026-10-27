using MediatR;
using Refahi.Modules.Entertainments.Domain;
using Refahi.Modules.Orders.Application.Contracts.IntegrationEvents;

namespace Refahi.Modules.Entertainments.Application.Features.Checkout;

public sealed class EntertainmentPurchasePaidNotificationHandler(IEntertainmentRepository repository, Refahi.Modules.Entertainments.Application.Contracts.Providers.IEntertainmentMutationLock gate) : INotificationHandler<OrderPaidIntegrationEvent>
{
    public async Task Handle(OrderPaidIntegrationEvent e, CancellationToken ct)
    {
        if (!e.SourceModule.Equals("Entertainments", StringComparison.OrdinalIgnoreCase) || !e.SourceReferenceId.HasValue)
            return;

        await using var held = await gate.AcquireAsync(e.SourceReferenceId.Value, ct);
        var value = await repository.GetFreshOrderAsync(e.SourceReferenceId.Value, ct);

        if (value is null || value.OrderId != e.OrderId || value.UserId != e.UserId)
            return;

        value.QueueFulfillment(e.PaymentId); await repository.SaveChangesAsync(ct);
    }
}
