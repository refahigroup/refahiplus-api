using Microsoft.Extensions.Logging;
using System.Security.Cryptography;
using MediatR;
using Refahi.Modules.Orders.Application.Contracts.Queries;
using Refahi.Modules.Pools.Application.Contracts;
using Refahi.Modules.Pools.Application.Contracts.Public;
using Refahi.Modules.Pools.Domain;
namespace Refahi.Modules.Pools.Application.Features.Purchases.Issue;
public sealed class IssuePoolVouchersHandler(IPoolPurchaseRepository repository, IPoolMutationLock gate,
    IPoolCodeProtector protector, IMediator mediator, TimeProvider clock, ILogger<IssuePoolVouchersHandler> logger) : IRequestHandler<IssuePoolVouchersCommand>
{
    public async Task<Unit> Handle(IssuePoolVouchersCommand r, CancellationToken ct)
    {
        await using var lease = await gate.AcquireAsync(r.Id, ct);
        var p = await repository.GetAsync(r.Id, ct);
        if (p?.OrderId is not Guid orderId || p.Status != PoolPurchaseStatus.AwaitingPayment) return Unit.Value;
        var order = await mediator.Send(new GetOrderByIdQuery(orderId, p.UserId, "User"), ct);
        if (!PoolOrderVerification.Matches(p, order) || order!.PaymentState != "Paid" || order.Status == "Cancelled") return Unit.Value;
        var codes = p.Requests.ToDictionary(x => x.Id, _ =>
        {
            var code = Convert.ToHexString(RandomNumberGenerator.GetBytes(16)); return (PoolKeys.Hash(code), protector.Protect(code));
        });
        p.CommitPaid(codes, clock.GetUtcNow()); await repository.SaveAsync(ct); logger.LogInformation("Pool vouchers issued. PurchaseId={PurchaseId} OrderId={OrderId} Count={Count}",p.Id,p.OrderId,p.Requests.Count); return Unit.Value;
    }
}
