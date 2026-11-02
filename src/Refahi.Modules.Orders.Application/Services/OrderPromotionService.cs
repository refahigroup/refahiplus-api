using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MediatR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Refahi.Modules.Orders.Application.Contracts.Commands;
using Refahi.Modules.Orders.Application.Contracts.Cancellation;
using Refahi.Modules.Orders.Domain.Aggregates;
using Refahi.Modules.Orders.Domain.Entities;
using Refahi.Modules.Orders.Domain.Enums;
using Refahi.Modules.Orders.Domain.Exceptions;
using Refahi.Modules.Orders.Domain.Repositories;
using Refahi.Modules.Promotions.Application.Contracts;
using Refahi.Modules.Wallets.Application.Contracts.Queries;
using Refahi.Modules.Wallets.Application.Contracts.Features.GetWalletInfo;
using Refahi.Modules.Wallets.Application.Contracts.Features.ReleasePaymentIntent;

namespace Refahi.Modules.Orders.Application.Services;

public sealed class OrderPromotionService(IOrderRepository orders, IOrderPromotionOperationStore operations,
    ISender sender, IPromotionCodeProtector protector, IConfiguration configuration, TimeProvider clock,
    ILogger<OrderPromotionService> logger, IEnumerable<IOrderCancellationParticipant> cancellationParticipants)
{
    public async Task<AppliedOrderPromotionDto> PreviewAsync(Order order, string code, long version, CancellationToken ct)
    {
        EnsureEnabled(); EnsureMutable(order, version);
        var quote = await sender.Send(new PreviewPromotionQuery(code, Context(order)), ct);
        return new(order.Id, order.PricingVersion, order.AmountBeforePromotionMinor, quote.DiscountMinor,
            checked(order.AmountBeforePromotionMinor - quote.DiscountMinor), null, quote.ExpiresAt, quote.Items.Select(x => new OrderPromotionItemDto(x.OrderItemId, x.AmountMinor)).ToArray());
    }
    public async Task<AppliedOrderPromotionDto> MutateAsync(Order order, string? code, long version, string key, CancellationToken ct)
    {
        var normalized = code?.Trim().ToUpperInvariant();
        var fingerprint = Hash(JsonSerializer.Serialize(new { Code = normalized, Version = version }));
        var existing = await operations.FindAsync(order.Id, key, ct);
        if (existing != null)
        {
            if (existing.Fingerprint != fingerprint) throw new OrderStateConflictException("کلید درخواست با محتوای دیگری استفاده شده است");
            if (existing.ResultJson != null)
            {
                using var saved = JsonDocument.Parse(existing.ResultJson);
                if (saved.RootElement.TryGetProperty("Error", out var error)) throw new PromotionException(error.GetString()!, saved.RootElement.GetProperty("StatusCode").GetInt32());
                return JsonSerializer.Deserialize<AppliedOrderPromotionDto>(existing.ResultJson)!;
            }
            await ResumeOperationAsync(order, existing, ct);
            return Current(order);
        }
        if (order.PromotionReservationId.HasValue) await ReconcileAsync(order, ct);
        if (code != null) EnsureEnabled();
        EnsureMutable(order, version);
        if (order.PendingPromotionOperationId.HasValue) throw new OrderStateConflictException("عملیات قبلی تخفیف هنوز تکمیل نشده است");
        if (code != null && order.PromotionReservationId.HasValue)
        {
            if (order.PromotionCodeHash == Hash(normalized!)) return Current(order);
            throw new OrderStateConflictException("ابتدا کد تخفیف فعلی را حذف کنید");
        }
        var operation = OrderPromotionOperation.Create(order.Id, key, fingerprint, code == null ? "Remove" : "Apply", normalized == null ? null : protector.Protect(normalized), clock.GetUtcNow());
        operations.Add(operation); order.StartPromotionOperation(operation.Id); await orders.UpdateAsync(order, ct);
        await ResumeOperationAsync(order, operation, ct); return Current(order);
    }
    public async Task ResumeOperationAsync(Order order, OrderPromotionOperation operation, CancellationToken ct)
    {
        if (operation.ResultJson != null) return;
        if (operation.Kind == "Apply")
        {
            var walletId = Guid.TryParse(configuration["Promotions:FundingWalletId"], out var configuredWallet) ? configuredWallet : Guid.Empty;
            var wallet = walletId == Guid.Empty ? null : await sender.Send(new GetWalletInfoByIdQuery(walletId), ct);
            if (wallet is null || wallet.WalletType != 1 || wallet.Status != 1 || wallet.Currency != "IRR")
            { await FailOperation(order, operation, ct, "کیف بودجهٔ رفاهی تنظیم نشده یا معتبر نیست", 409); throw new PromotionException("کیف بودجهٔ رفاهی تنظیم نشده یا معتبر نیست", 409); }
            PromotionReservation reservation;
            try { reservation = await sender.Send(new ReservePromotionCommand(protector.Unprotect(operation.CodeCiphertext!), Context(order), operation.Id.ToString("N")), ct); }
            catch (PromotionException ex) { await FailOperation(order, operation, ct, ex.Message, ex.StatusCode); throw; }
            // Persisted operation keys make a reserve replay safe after either side restarts.
            if (reservation.Status != "Reserved" || reservation.Quote.ExpiresAt <= clock.GetUtcNow() || order.PayableUntil <= clock.GetUtcNow())
            {
                if (reservation.Status == "Reserved") await sender.Send(new ReleasePromotionCommand(reservation.Id, order.Id, true), ct);
                await FailOperation(order, operation, ct, "مهلت رزرو تخفیف به پایان رسیده است", 409); throw new OrderStateConflictException("مهلت رزرو تخفیف به پایان رسیده است");
            }
            var raw = protector.Unprotect(operation.CodeCiphertext!);
            if (order.Status != OrderStatus.Pending)
            {
                await sender.Send(new ReleasePromotionCommand(reservation.Id, order.Id), ct);
                await FailOperation(order, operation, ct); return;
            }
            order.ApplyPromotion(reservation.Id, reservation.Quote.CampaignId, walletId, reservation.Quote.DiscountMinor,
                reservation.Quote.ExpiresAt, JsonSerializer.Serialize(reservation.Quote), Hash(raw), "••••" + raw[^Math.Min(raw.Length, 4)..]);
        }
        else
        {
            if (order.PromotionReservationId.HasValue) await sender.Send(new ReleasePromotionCommand(order.PromotionReservationId.Value, order.Id), ct);
            if (order.Status == OrderStatus.Pending) order.RemovePromotion(); else order.FinishPromotionOperation();
        }
        operation.Complete(JsonSerializer.Serialize(Current(order))); await orders.UpdateAsync(order, ct);
        logger.LogInformation("Promotion operation completed. OrderId={OrderId} RedemptionId={RedemptionId} CampaignId={CampaignId}", order.Id, order.PromotionReservationId, order.PromotionCampaignId);
    }
    private async Task FailOperation(Order order, OrderPromotionOperation operation, CancellationToken ct, string? error = null, int status = 409)
    { order.FinishPromotionOperation(); operation.Complete(error == null ? JsonSerializer.Serialize(Current(order)) : JsonSerializer.Serialize(new { Error = error, StatusCode = status })); await orders.UpdateAsync(order, ct); }

    public async Task PreparePaymentAsync(Order order, long? expectedVersion, CancellationToken ct)
    {
        if (order.PendingPromotionOperationId.HasValue) throw new OrderStateConflictException("عملیات تخفیف هنوز تکمیل نشده است");
        if (!order.PromotionReservationId.HasValue) return;
        if (expectedVersion != order.PricingVersion) throw new OrderStateConflictException("مبلغ سفارش تغییر کرده است؛ دوباره بررسی کنید");
        await sender.Send(new BeginPromotionPaymentCommand(order.PromotionReservationId.Value, order.Id, order.UserId, order.PaymentIntentId.HasValue), ct);
    }
    public async Task ReconcileAsync(Order order, CancellationToken ct)
    {
        if (order.PendingPromotionOperationId.HasValue)
        {
            var operation = await operations.GetAsync(order.PendingPromotionOperationId.Value, ct);
            if (operation != null) await ResumeOperationAsync(order, operation, ct);
        }
        if (!order.PromotionReservationId.HasValue) return;
        var id = order.PromotionReservationId.Value;
        var known = await sender.Send(new FindOrderPaymentQuery(order.Id, $"order-reserve-{order.Id:N}"), ct);
        if (known != null && order.PaymentState == PaymentState.Unpaid)
        { order.MarkAsReserved(known.IntentId); await orders.UpdateAsync(order, ct); }
        if (known?.Status == 2 && known.PaymentId.HasValue)
        {
            if (order.PaymentState == PaymentState.Unpaid) order.MarkAsReserved(known.IntentId);
            if (order.PaymentState == PaymentState.Reserved) order.MarkAsPaid(known.PaymentId.Value);
            if (known.Refunded && order.PaymentState == PaymentState.Paid) { order.Cancel(); order.MarkAsRefunded(); order.RecordCancellation("Refunded"); }
            await orders.UpdateAsync(order, ct);
        }
        if (known?.Status == 3 && order.PaymentState is PaymentState.Unpaid or PaymentState.Reserved)
        {
            if (order.PaymentState == PaymentState.Unpaid) order.MarkAsReserved(known.IntentId);
            if (order.Status != OrderStatus.Cancelled) order.Cancel();
            order.MarkAsReleased(); order.RecordCancellation("Released"); await orders.UpdateAsync(order, ct);
        }
        if (order.PaymentState == PaymentState.Refunded || known?.Refunded == true)
            await sender.Send(new ReversePromotionCommand(id, order.Id), ct);
        else if (order.PaymentState == PaymentState.Paid && order.PaymentId.HasValue)
            await sender.Send(new CompletePromotionCommand(id, order.Id, order.PaymentId.Value), ct);
        else if (order.Status == OrderStatus.Cancelled || order.PaymentState == PaymentState.Released)
        {
            if (known?.Status == 1) await ReleaseIntent(known.IntentId, order.Id, ct);
            if (known == null || known.Status != 2) await sender.Send(new ReleasePromotionCommand(id, order.Id), ct);
        }
        else if (order.PromotionExpiresAt <= clock.GetUtcNow())
        {
            if (known?.Status == 1)
            {
                var participant = cancellationParticipants.FirstOrDefault(x => x.SourceModule.Equals(order.SourceModule, StringComparison.OrdinalIgnoreCase));
                if (participant != null)
                {
                    var preparation = await participant.PrepareAsync(new OrderCancellationContext(order.Id, order.UserId, order.SourceModule,
                        order.SourceReferenceId, order.ReferenceType, order.PaymentState.ToString(), "مهلت پرداخت با تخفیف به پایان رسیده است"), ct);
                    if (!preparation.CanContinue) throw new OrderStateConflictException(preparation.Message ?? "لغو سرویس هنوز تکمیل نشده است");
                }
                // Once money was held, cancel this order instead of reusing a released intent key.
                await ReleaseIntent(known.IntentId, order.Id, ct);
                if (order.PaymentState == PaymentState.Unpaid) order.MarkAsReserved(known.IntentId);
                order.Cancel(); order.MarkAsReleased(); order.RecordCancellation("Released"); await orders.UpdateAsync(order, ct);
                await sender.Send(new ReleasePromotionCommand(id, order.Id, true), ct);
            }
            else if (known == null && order.PaymentState == PaymentState.Unpaid)
            {
                await sender.Send(new ReleasePromotionCommand(id, order.Id, true), ct);
                order.RemovePromotion(); await orders.UpdateAsync(order, ct);
            }
        }
    }
    private async Task ReleaseIntent(Guid intent, Guid order, CancellationToken ct)
    {
        var result = await sender.Send(new ReleasePaymentIntentCommand(intent, $"order-release-{order:N}"), ct);
        if (result.Status != Refahi.Modules.Wallets.Application.Contracts.CommandStatus.Completed) throw new OrderStateConflictException("آزادسازی مبلغ هنوز تکمیل نشده است");
    }
    private void EnsureMutable(Order order, long version)
    { order.EnsurePromotionMutable(); if (order.PricingVersion != version || order.PayableUntil <= clock.GetUtcNow()) throw new OrderStateConflictException("نسخه یا مهلت سفارش معتبر نیست"); }
    private void EnsureEnabled() { if (!bool.TryParse(configuration["Promotions:Enabled"], out var enabled) || !enabled) throw new PromotionException("امکان اعمال کد تخفیف در حال حاضر فعال نیست", 409); }
    private static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    private static PromotionContext Context(Order order) => new(order.Id, order.UserId, order.SourceModule, order.AmountBeforePromotionMinor,
        order.Items.Select(x => new PromotionItemInput(x.Id, x.CategoryCode, x.FinalPriceMinor)).ToArray(), order.PayableUntil);
    public static AppliedOrderPromotionDto Current(Order order)
    {
        var quote = order.PromotionSnapshotJson == null ? null : JsonSerializer.Deserialize<PromotionQuote>(order.PromotionSnapshotJson);
        return new(order.Id, order.PricingVersion, order.AmountBeforePromotionMinor, order.DiscountCodeAmountMinor, order.FinalAmountMinor,
            order.PromotionReservationId, order.PromotionExpiresAt, quote?.Items.Select(x => new OrderPromotionItemDto(x.OrderItemId, x.AmountMinor)).ToArray() ?? []);
    }
}
