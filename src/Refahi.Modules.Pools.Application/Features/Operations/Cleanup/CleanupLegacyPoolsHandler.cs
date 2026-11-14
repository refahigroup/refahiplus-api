using MediatR;
using Microsoft.Extensions.Hosting;
using Refahi.Modules.Orders.Application.Contracts.Commands;
using Refahi.Modules.Orders.Application.Contracts.Queries;
using Refahi.Modules.Pools.Application.Contracts;
using Refahi.Modules.Pools.Application.Contracts.Public;
namespace Refahi.Modules.Pools.Application.Features.Operations.Cleanup;
public sealed class CleanupLegacyPoolsHandler(ILegacyPoolStore legacy, IMediator mediator, IHostEnvironment environment,
    IPoolMutationLock gate) : IRequestHandler<CleanupLegacyPoolsCommand, int>
{
    public async Task<int> Handle(CleanupLegacyPoolsCommand r, CancellationToken ct)
    {
        if (!environment.IsDevelopment() && !environment.IsEnvironment("Test") && !environment.IsStaging())
            throw new PoolException("پاک‌سازی فقط در محیط آزمایشی مجاز است", 403);
        await using var lease = await gate.AcquireAsync(PoolKeys.Lock("legacy-cleanup"), ct);
        var records = await legacy.ListAsync(ct);
        foreach (var b in records)
        {
            var order = b.OrderId.HasValue ? await mediator.Send(new GetOrderByIdQuery(b.OrderId.Value, b.UserId, "User"), ct)
                : await mediator.Send(new GetOrderByIdempotencyKeyQuery($"pools-order:{b.Id:N}", b.UserId, "Pools"), ct);
            if (b.OrderId.HasValue && order == null) throw new PoolException("سفارش مالی قدیمی یافت نشد؛ پاک‌سازی متوقف شد");
            if (order != null)
            {
                if (order.SourceModule != "Pools" || order.ReferenceType != "PoolBooking" || order.SourceReferenceId != b.Id)
                    throw new PoolException("مرجع مالی قدیمی نیازمند بررسی است");
                await mediator.Send(new CancelOrderCommand(order.Id, "پاک‌سازی خرید آزمایشی استخر", $"pools-legacy-cancel:{b.Id:N}", CallerUserId: b.UserId, CallerRole: "User"), ct);
                var final = await mediator.Send(new GetOrderByIdQuery(order.Id, b.UserId, "User"), ct);
                if (final?.Status is not ("Cancelled" or "Refunded") || final.PaymentState is "Paid" or "Reserved") throw new PoolException("تسویه خرید قدیمی کامل نشده است");
            }
            await legacy.DeleteAsync(b.Id, ct);
        }
        await legacy.CompleteCleanupAsync(ct); return records.Count;
    }
}
