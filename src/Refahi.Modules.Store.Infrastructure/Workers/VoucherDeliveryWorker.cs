using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Refahi.Modules.Identity.Application.Contracts.Queries;
using Refahi.Modules.Orders.Application.Contracts.IntegrationEvents;
using Refahi.Modules.Orders.Application.Contracts.Queries;
using Refahi.Modules.Store.Application.Contracts.Vouchers;
using Refahi.Modules.Store.Domain.Enums;
using Refahi.Modules.Store.Domain.Repositories;
using Refahi.Modules.Store.Infrastructure.Persistence.Context;
using Refahi.Shared.Services.Notification;

namespace Refahi.Modules.Store.Infrastructure.Workers;

public sealed class VoucherDeliveryWorker(
    IServiceScopeFactory scopeFactory,
    IConfiguration configuration,
    TimeProvider clock,
    ILogger<VoucherDeliveryWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessBatchAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
            catch (Exception ex)
            {
                logger.LogError(ex, "Voucher delivery worker cycle failed");
            }
            await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
        }
    }

    internal async Task ProcessBatchAsync(CancellationToken ct)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<StoreDbContext>();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        var notifications = scope.ServiceProvider.GetRequiredService<INotificationService>();
        var protector = scope.ServiceProvider.GetRequiredService<IVoucherCodeProtector>();
        var now = clock.GetUtcNow();
        var maxRetries = configuration.GetValue<int?>("Store:Vouchers:DeliveryMaxRetryCount") ?? 10;
        await ReconcileReservationsAsync(db, mediator, scope.ServiceProvider, now, ct);

        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        var pending = (short)VoucherDeliveryStatus.Pending;
        var retry = (short)VoucherDeliveryStatus.Retry;
        var rows = await db.VoucherDeliveries
            .FromSqlInterpolated($@"
                SELECT * FROM store.voucher_deliveries
                WHERE ""Status"" IN ({pending}, {retry})
                  AND ""NextAttemptAtUtc"" <= {now}
                ORDER BY ""NextAttemptAtUtc"", ""Id""
                FOR UPDATE SKIP LOCKED
                LIMIT {50}")
            .ToListAsync(ct);
        if (rows.Count == 0)
        {
            await transaction.CommitAsync(ct);
            return;
        }

        var userIds = rows.Select(x => x.UserId).Distinct().ToArray();
        var users = (await mediator.Send(new GetOrderUserSummariesQuery(userIds), ct))
            .ToDictionary(x => x.UserId);
        foreach (var delivery in rows)
        {
            try
            {
                if (!users.TryGetValue(delivery.UserId, out var user)
                    || string.IsNullOrWhiteSpace(user.MobileNumber))
                {
                    delivery.MarkNoRecipient();
                    continue;
                }
                var voucher = await db.Vouchers.AsNoTracking()
                    .SingleOrDefaultAsync(x => x.Id == delivery.VoucherId, ct);
                if (voucher is null || !protector.TryUnprotect(voucher.CodeCiphertext, out var code))
                {
                    delivery.MarkFailed("اطلاعات ووچر برای ارسال در دسترس نیست", now, maxRetries);
                    continue;
                }
                var expiry = voucher.ExpiresAtUtc.HasValue
                    ? $"، اعتبار تا {voucher.ExpiresAtUtc.Value:yyyy/MM/dd}"
                    : string.Empty;
                var body = $"رفاهی پلاس؛ {voucher.ProductTitle}، کد: {code}، سفارش: {voucher.OrderNumber}{expiry}";
                await notifications.SendSms([user.MobileNumber], body, cancellationToken: ct);
                delivery.MarkSent(clock.GetUtcNow());
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                delivery.MarkFailed("ارسال پیامک ناموفق بود", clock.GetUtcNow(), maxRetries);
                logger.LogWarning("Voucher SMS delivery failed. DeliveryId={DeliveryId}, Attempt={Attempt}",
                    delivery.Id, delivery.AttemptCount);
            }
        }
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
    }

    private async Task ReconcileReservationsAsync(
        StoreDbContext db,
        IMediator mediator,
        IServiceProvider services,
        DateTimeOffset now,
        CancellationToken ct)
    {
        var grace = configuration.GetValue<int?>("Store:Vouchers:ReservationCleanupGraceMinutes") ?? 5;
        var threshold = now.AddMinutes(-Math.Max(0, grace));
        var storeOrderIds = await db.VoucherCodeAllocations.AsNoTracking()
            .Where(x => x.Status == VoucherCodeAllocationStatus.Reserved
                && x.ReservedUntilUtc <= threshold)
            .Select(x => x.StoreOrderId)
            .Distinct()
            .Take(25)
            .ToListAsync(ct);
        var orders = services.GetRequiredService<IStoreOrderRepository>();
        foreach (var storeOrderId in storeOrderIds)
        {
            var storeOrder = await orders.GetByIdAsync(storeOrderId, ct);
            if (storeOrder is null)
                continue;
            if (!storeOrder.OrderId.HasValue)
            {
                storeOrder.MarkCancelled();
                await orders.UpdateAsync(storeOrder, ct);
                continue;
            }
            var order = await mediator.Send(
                new GetOrderByIdQuery(storeOrder.OrderId.Value, Guid.Empty, "Admin"), ct);
            if (order is null || order.Status is "Pending" or "Cancelled")
            {
                storeOrder.MarkCancelled();
                await orders.UpdateAsync(storeOrder, ct);
                continue;
            }
            if (order.Status is "Confirmed" or "Processing" or "Shipped" or "Delivered"
                && storeOrder.Status != StoreOrderStatus.Paid)
            {
                logger.LogWarning(
                    "Recovering paid Store order with unfinished vouchers. StoreOrderId={StoreOrderId}, OrderId={OrderId}",
                    storeOrder.Id, order.Id);
                await mediator.Publish(new OrderPaidIntegrationEvent(
                    order.Id, order.OrderNumber, order.UserId, "Store", storeOrder.Id,
                    "StoreOrder", null, Guid.Empty, order.FinalAmountMinor, now), ct);
            }
        }
    }
}
