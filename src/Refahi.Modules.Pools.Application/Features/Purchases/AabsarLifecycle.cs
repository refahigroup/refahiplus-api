using System.Text.Json;
using Microsoft.Extensions.Logging;
using Refahi.Modules.Pools.Application.Contracts;
using Refahi.Modules.Pools.Application.Contracts.Public;
using Refahi.Modules.Pools.Domain;

namespace Refahi.Modules.Pools.Application.Features.Purchases;

public sealed class AabsarLifecycle(IAabsarRepository store, IAabsarGateway gateway, IPoolCodeProtector protector,
    TimeProvider clock, ILogger<AabsarLifecycle> logger)
{
    public bool SalesEnabled => gateway.Options.Enabled;

    public async Task ValidateAsync(PoolPurchase purchase, CancellationToken ct)
    {
        var rows = await store.ShowtimesAsync(purchase.Requests.Select(x => x.SessionId), ct);
        foreach (var group in purchase.Requests.GroupBy(x => x.SessionId))
        {
            if (!rows.TryGetValue(group.Key, out var row) || !row.IsActive || row.StartsAt <= clock.GetUtcNow() ||
                group.Any(x => row.StartsAt != x.StartsAtUtc))
                throw new PoolException("زمان سانس تغییر کرده است", 409, "SESSION_TIME_CHANGED");
            var quote = await gateway.QuoteAsync(row.ShowtimeId, ct);
            var count = group.Sum(x => x.OrderQuantity);
            if (group.Any(x => AabsarCheckout.Price(quote, x.TicketType!, count).Price != x.OrderUnitPrice))
                throw new PoolException("قیمت تغییر کرده است؛ خرید جدید را تأیید کنید", 409, "PRICE_CHANGED");
        }
    }

    // Called with the purchase mutation lock held, after verifying the financial order.
    public async Task IssueAsync(PoolPurchase p, CancellationToken ct)
    {
        var f = await RequireAsync(p.Id, ct);
        if (p.Status == PoolPurchaseStatus.AwaitingPayment) p.RecordExternalPayment();
        if (f.Status == AabsarOperation.Sending)
        {
            if (f.ReceiptCiphertext is not null)
            {
                var saved = JsonSerializer.Deserialize<AabsarReceipt>(protector.Unprotect(f.ReceiptCiphertext))!;
                await FinishIssueAsync(p, f, saved, ct);
                return;
            }
            f.Resolve(AabsarOperation.IssueUnknown);
            p.FinishAabsarIssuance(PoolReservationStatus.Unknown);
            await store.SaveAsync(ct);
            return;
        }
        if (f.Status == AabsarOperation.Cancelling)
        {
            f.Resolve(AabsarOperation.CancelUnknown);
            await store.SaveAsync(ct);
        }
        if (f.Status != AabsarOperation.Ready) return;
        var rows = await store.ShowtimesAsync(p.Requests.Select(x => x.SessionId), ct);
        if (p.Requests.Any(x => !rows.ContainsKey(x.SessionId)) || f.ContactCiphertext is null)
        {
            await FinishIssueAsync(p, f, null, ct);
            return;
        }
        var contact = JsonSerializer.Deserialize<AabsarContact>(protector.Unprotect(f.ContactCiphertext))!;
        var selections = p.Requests.GroupBy(x => x.SessionId).OrderBy(x => x.Key)
            .Select(g => new AabsarSelection(rows[g.Key].ShowtimeId, g.Sum(x => x.AdultCount), g.Sum(x => x.ChildCount))).ToArray();
        f.BeginIssue(PoolKeys.Hash(JsonSerializer.Serialize(new { contact, selections })));
        await store.SaveAsync(ct);
        AabsarReceipt? receipt;
        try
        {
            receipt = await gateway.IssueAsync(contact, selections, ct);
            if (receipt is not null)
            {
                ValidateReceipt(p, receipt);
                if (receipt.TotalMinor is long received && received != p.TotalMinor)
                    logger.LogWarning("Aabsar supplier total differs from source amount. PurchaseId={PurchaseId} OrderId={OrderId}",
                        p.Id, p.OrderId);
            }
        }
        catch (Exception ex)
        {
            f.Resolve(AabsarOperation.IssueUnknown);
            p.FinishAabsarIssuance(PoolReservationStatus.Unknown);
            await store.SaveAsync(CancellationToken.None);
            logger.LogWarning("Aabsar issue outcome unknown. PurchaseId={PurchaseId} OrderId={OrderId} ErrorType={ErrorType}", p.Id, p.OrderId, ex.GetType().Name);
            return;
        }
        if (receipt is not null)
        {
            f.Resolve(AabsarOperation.Sending, protector.Protect(JsonSerializer.Serialize(receipt)));
            await store.SaveAsync(CancellationToken.None);
        }
        await FinishIssueAsync(p, f, receipt, CancellationToken.None);
    }

    public async Task<string?> CancelAsync(PoolPurchase p, bool paid, CancellationToken ct)
    {
        var f = await RequireAsync(p.Id, ct);
        if (f.Status == AabsarOperation.Cancelling)
        {
            f.Resolve(AabsarOperation.CancelUnknown);
            await store.SaveAsync(ct);
        }
        if (f.Status is AabsarOperation.Sending or AabsarOperation.IssueUnknown or AabsarOperation.CancelUnknown ||
            paid && f.Status == AabsarOperation.Ready)
            return "نتیجه عملیات تأمین‌کننده نیازمند بررسی است";
        if (f.Status != AabsarOperation.Issued) return null;

        var receipt = JsonSerializer.Deserialize<AabsarReceipt>(protector.Unprotect(f.ReceiptCiphertext!))!;
        var adults = receipt.Tickets.Count(x => !x.IsChild);
        var children = receipt.Tickets.Count - adults;
        f.BeginCancel(PoolKeys.Hash($"{receipt.OrderCode}:{adults}:{children}"));
        await store.SaveAsync(ct);
        bool cancelled;
        try { cancelled = await gateway.CancelAsync(receipt.OrderCode, adults, children, ct); }
        catch (Exception ex)
        {
            f.Resolve(AabsarOperation.CancelUnknown);
            await store.SaveAsync(CancellationToken.None);
            logger.LogWarning("Aabsar cancel outcome unknown. PurchaseId={PurchaseId} OrderId={OrderId} ErrorType={ErrorType}", p.Id, p.OrderId, ex.GetType().Name);
            return "نتیجه لغو آبسار نیازمند بررسی است";
        }
        if (!cancelled)
        {
            f.Resolve(AabsarOperation.Issued);
            await store.SaveAsync(CancellationToken.None);
            return "لغو بلیت توسط آبسار تأیید نشد";
        }
        f.Resolve(AabsarOperation.Cancelled);
        p.ConfirmAabsarCancellation();
        await store.SaveAsync(CancellationToken.None);
        return null;
    }

    public async Task ResolveAsync(PoolPurchase purchase, AabsarResolution input, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(input.Evidence) || input.Evidence.Length > 2000)
            throw new PoolException("مستند بررسی الزامی است", 400);
        var f = await RequireAsync(purchase.Id, ct);
        var evidence = PoolKeys.Hash(JsonSerializer.Serialize(input));
        if (f.EvidenceFingerprint == evidence) return;
        if (purchase.Status != PoolPurchaseStatus.Paid)
            throw new PoolException("سفارش پرداخت‌شده برای بررسی یافت نشد", 409);
        switch (input.Outcome)
        {
            case "fulfilled" when f.Status is AabsarOperation.IssueUnknown or AabsarOperation.Sending && input.Receipt is not null:
                ValidateReceipt(purchase, input.Receipt);
                f.Resolve(AabsarOperation.Issued, protector.Protect(JsonSerializer.Serialize(input.Receipt)), evidence);
                purchase.FinishAabsarIssuance(PoolReservationStatus.Reserved);
                break;
            case "notcreated" when f.Status is AabsarOperation.IssueUnknown or AabsarOperation.Sending:
                f.Resolve(AabsarOperation.Rejected, evidence: evidence);
                purchase.FinishAabsarIssuance(PoolReservationStatus.Rejected);
                purchase.BeginCancellation();
                break;
            case "cancelled" when f.Status is AabsarOperation.CancelUnknown or AabsarOperation.Cancelling:
                f.Resolve(AabsarOperation.Cancelled, evidence: evidence);
                purchase.ConfirmAabsarCancellation();
                break;
            case "notcancelled" when f.Status is AabsarOperation.CancelUnknown or AabsarOperation.Cancelling:
                f.Resolve(AabsarOperation.Issued, evidence: evidence);
                break;
            default:
                throw new PoolException("نتیجه بررسی با وضعیت عملیات سازگار نیست", 409);
        }
        await store.SaveAsync(ct);
        logger.LogInformation("Aabsar operation resolved. PurchaseId={PurchaseId} OrderId={OrderId} Status={Status}",
            purchase.Id, purchase.OrderId, f.Status);
    }

    public async Task<object> OrderViewAsync(PoolPurchase p, bool reveal, CancellationToken ct)
    {
        var f = await RequireAsync(p.Id, ct);
        var status = p.Status switch
        {
            PoolPurchaseStatus.AwaitingPayment => "PendingPayment",
            PoolPurchaseStatus.Cancelled => "Cancelled",
            PoolPurchaseStatus.Refunded => "Refunded",
            PoolPurchaseStatus.CancellationPending => "CompensationPending",
            _ => f.Status switch
            {
                AabsarOperation.Issued => "Completed",
                AabsarOperation.IssueUnknown or AabsarOperation.CancelUnknown => "ManualReview",
                AabsarOperation.Cancelling => "CancellationPending",
                _ => "FulfillmentPending"
            }
        };
        var canReveal = f.Status == AabsarOperation.Issued && reveal;
        var receipt = f.Status != AabsarOperation.Issued || f.ReceiptCiphertext is null ? null :
            JsonSerializer.Deserialize<AabsarReceipt>(protector.Unprotect(f.ReceiptCiphertext));
        return new
        {
            p.Id, p.OrderId, Status = status, TotalAmountMinor = p.TotalMinor, p.PayableUntil,
            CanCancel = status is "PendingPayment" or "Completed",
            Items = p.Requests.Select(x => new { x.Id, x.ProviderKey, SellerKey = "aabsar", Title = x.PoolName,
                OfferTitle = x.Date + " " + x.Time, PurchaseOptionKey = x.TicketType,
                Quantity = x.OrderQuantity, UnitPriceMinor = x.OrderUnitPrice, CategoryCode = PoolKeys.Category }).ToArray(),
            Fulfillments = new[] { new { Id = p.Id, ProviderKey = "aabsar", Status = status,
                ProviderOrderCode = canReveal ? receipt?.OrderCode : null, FailureReason = (string?)null,
                Tickets = receipt?.Tickets.Select(x => canReveal ? x : x with { Code = null }).ToArray() ?? [] } }
        };
    }

    private async Task<AabsarFulfillment> RequireAsync(Guid id, CancellationToken ct)
        => await store.FulfillmentAsync(id, ct) ?? throw new PoolException("عملیات آبسار یافت نشد", 404);

    private async Task FinishIssueAsync(PoolPurchase p, AabsarFulfillment f, AabsarReceipt? receipt, CancellationToken ct)
    {
        f.Resolve(receipt is null ? AabsarOperation.Rejected : AabsarOperation.Issued,
            receipt is null || f.ReceiptCiphertext is not null ? null : protector.Protect(JsonSerializer.Serialize(receipt)));
        p.FinishAabsarIssuance(receipt is null ? PoolReservationStatus.Rejected : PoolReservationStatus.Reserved);
        if (receipt is null) p.BeginCancellation();
        await store.SaveAsync(ct);
        logger.LogInformation("Aabsar issue recorded. PurchaseId={PurchaseId} OrderId={OrderId} Status={Status}",
            p.Id, p.OrderId, f.Status);
    }

    private static void ValidateReceipt(PoolPurchase p, AabsarReceipt receipt)
    {
        if (string.IsNullOrWhiteSpace(receipt.OrderCode) || receipt.Tickets is null ||
            receipt.Tickets.Count(x => x.IsChild) != p.Requests.Sum(x => x.ChildCount) ||
            receipt.Tickets.Count(x => !x.IsChild) != p.Requests.Sum(x => x.AdultCount) ||
            receipt.Tickets.Any(x => string.IsNullOrWhiteSpace(x.Code)) ||
            receipt.Tickets.Select(x => x.Code).Distinct(StringComparer.Ordinal).Count() != receipt.Tickets.Count)
            throw new PoolException("پاسخ صدور بلیت آبسار کامل نیست", 409);
    }
}
