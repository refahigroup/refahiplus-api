using MediatR;
using Refahi.Modules.Orders.Application.Contracts.IntegrationEvents;
using Refahi.Modules.Pools.Application.Contracts.Public;
namespace Refahi.Modules.Pools.Application.Features.Purchases.Cancel;
public sealed class PoolOrderCancelledHandler(IMediator mediator) : INotificationHandler<OrderCancelledIntegrationEvent>
{
    public async Task Handle(OrderCancelledIntegrationEvent e, CancellationToken ct)
    { if (e.SourceModule == "Pools" && e.ReferenceType == "PoolPurchase" && e.SourceReferenceId.HasValue) await mediator.Send(new ReconcilePoolPurchaseCommand(e.SourceReferenceId.Value), ct); }
}
