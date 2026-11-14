using MediatR;
using Refahi.Modules.Orders.Application.Contracts.IntegrationEvents;
using Refahi.Modules.Pools.Application.Contracts.Public;
namespace Refahi.Modules.Pools.Application.Features.Purchases.Issue;
public sealed class PoolOrderPaidHandler(IMediator mediator) : INotificationHandler<OrderPaidIntegrationEvent>
{
    public async Task Handle(OrderPaidIntegrationEvent e, CancellationToken ct)
    { if (e.SourceModule == "Pools" && e.ReferenceType == "PoolPurchase" && e.SourceReferenceId.HasValue) await mediator.Send(new IssuePoolVouchersCommand(e.SourceReferenceId.Value), ct); }
}
