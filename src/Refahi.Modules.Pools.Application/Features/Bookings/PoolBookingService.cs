using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Refahi.Modules.Orders.Application.Contracts.Commands;
using Refahi.Modules.Orders.Application.Contracts.Queries;
using Refahi.Modules.Pools.Application.Contracts;
using Refahi.Modules.Pools.Domain;

namespace Refahi.Modules.Pools.Application.Features.Bookings;

public sealed record PoolBookingSnapshot(PoolSelection Selection, PoolSummary? Pool, PoolSessionRow Row, PoolQuote Quote);

public sealed class PoolBookingService(IPoolBookingRepository repository, IPoolProviderFactory providers,
    IPoolCatalogStore catalog, IPoolMutationLock gate, IPoolPurchasePolicy policy, IMediator mediator,
    IOptions<PoolsOptions> options, ILogger<PoolBookingService> logger)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    public static PoolBookingSnapshot Snapshot(PoolBooking b) => JsonSerializer.Deserialize<PoolBookingSnapshot>(b.SnapshotJson, Json)!;
    public async Task<PoolBooking> Owned(Guid id, Guid user, CancellationToken ct)
    {
        var b = await repository.GetAsync(id, ct) ?? throw new PoolException("درخواست خرید یافت نشد", 404);
        if (b.UserId != user) throw new PoolException("دسترسی به این درخواست مجاز نیست", 403);
        return b;
    }
    public PoolBookingView View(PoolBooking b)
    {
        var s = Snapshot(b); var capabilities = providers.Get(b.ProviderKey).Capabilities;
        var allowed = options.Value.PurchaseEnabled && (!b.IsMock || options.Value.MockLifecycleEnabled && options.Value.TestFundsConfirmed);
        return new(b.Id, b.OrderId, b.OrderNumber, b.Version, s.Selection, s.Pool, s.Row, b.TotalMinor > 0 ? b.TotalMinor : s.Quote.TotalMinor ?? 0,
            b.PaymentStatus, b.ReservationStatus, b.IssuanceStatus, b.CancellationStatus, b.PayableUntil,
            allowed && b.IsPayable(DateTimeOffset.UtcNow), allowed && capabilities.Cancel && b.ProviderReference != null
                && b.CancellationStatus != "Cancelled", b.IsMock, b.HasTicket ? b.TicketCode : null, b.Message);
    }
    public async Task<PoolBookingView> GetAsync(Guid id, Guid user, CancellationToken ct)
    {
        var b = await Owned(id, user, ct);
        if (b.OrderId.HasValue)
        {
            var financial = await mediator.Send(new GetOrderByIdQuery(b.OrderId.Value, user, "User"), ct);
            if (financial?.PaymentState == "Paid" && b.PaymentStatus == "Unpaid" || financial?.PaymentState == "Refunded" && b.PaymentStatus != "Refunded"
                || financial?.Status == "Cancelled" && !b.OrderCancellationCompleted)
            {
                await using var lease = await gate.AcquireAsync(id, ct); b = await Owned(id, user, ct);
                if (financial.PaymentState == "Paid" && b.PaymentStatus == "Unpaid") b.MarkPaid();
                if (financial.PaymentState == "Refunded") b.MarkRefunded();
                if (financial.Status == "Cancelled") b.MarkOrderCancellationCompleted();
                await repository.SaveAsync(ct);
            }
        }
        return View(b);
    }
    public async Task<PoolBookingView> ReserveAsync(ReservePoolBookingCommand r, CancellationToken ct)
    {
        var p = providers.Get(r.Selection.ProviderKey); policy.EnsureAllowed(p.Capabilities.IsMock);
        if (!p.Capabilities.Reserve || p is not IPoolReservationProvider reservation) throw new PoolException("رزرو این تأمین‌کننده فعال نیست", 503);
        var selection = r.Selection with { ProviderKey = p.Key };
        var fingerprint = Hash(JsonSerializer.Serialize(new { selection, r.ExpectedTotalMinor }, Json));
        var lockId = new Guid(SHA256.HashData(Encoding.UTF8.GetBytes($"{r.UserId:N}:{r.IdempotencyKey}"))[..16]);
        await using var lease = await gate.AcquireAsync(lockId, ct);
        var old = await repository.FindAsync(r.UserId, r.IdempotencyKey, ct);
        if (old != null)
        {
            if (old.Fingerprint != fingerprint) throw new PoolException("کلید درخواست برای انتخاب دیگری استفاده شده است");
            return View(old);
        }
        var rows = await p.SessionsAsync(selection.PoolId, ct);
        var row = rows.SelectMany(x => x.Rows).SingleOrDefault(x => x.Id == selection.SessionRowId)
            ?? throw new PoolException("سانس انتخابی یافت نشد", 404);
        if (!row.IsSelling || selection.AdultCount > 0 && !row.AdultEnabled || selection.ChildCount > 0 && !row.ChildEnabled)
            throw new PoolException("فروش بلیط انتخابی غیرفعال است");
        var quote = await p.QuoteAsync(selection, ct);
        if (quote.TotalMinor is not long amount || amount <= 0) throw new PoolException("واحد پول تأمین‌کننده هنوز تأیید نشده است", 503);
        if (amount != r.ExpectedTotalMinor) throw new PoolException("قیمت تغییر کرده است؛ مبلغ جدید را بررسی کنید");
        var snapshot = new PoolBookingSnapshot(selection, await catalog.GetAsync(p.Key, selection.PoolId, ct), row, quote);
        var b = PoolBooking.Create(r.UserId, p.Key, r.IdempotencyKey, fingerprint, JsonSerializer.Serialize(snapshot, Json), p.Capabilities.IsMock);
        await using var bookingLease = await gate.AcquireAsync(b.Id, ct);
        var attempt = b.BeginAttempt("reserve"); await repository.AddAsync(b, ct);
        try
        {
            var result = await reservation.ReserveAsync(b.Id.ToString("N"), selection, quote, ct);
            ApplyReservation(b, result); attempt.Complete();
        }
        catch (PoolException) { attempt.Fail(false); b.FailReservation(); }
        catch (Exception ex) when (ex is PoolAmbiguousException or HttpRequestException or OperationCanceledException)
        { attempt.Fail(true); b.Review("نتیجه رزرو در حال بررسی است؛ درخواست جدید ثبت نکنید"); }
        await repository.SaveAsync(CancellationToken.None);
        logger.LogInformation("Pool reservation processed. BookingId={BookingId} Provider={Provider} ProviderReference={ProviderReference} Outcome={Outcome}",
            b.Id, b.ProviderKey, b.ProviderReference, b.ReservationStatus);
        return View(b);
    }
    private void ApplyReservation(PoolBooking b, PoolReservation result)
    {
        if (result.Status != "Reserved" || result.TotalMinor != Snapshot(b).Quote.TotalMinor || result.IsMock != b.IsMock)
            throw new PoolAmbiguousException();
        b.Reserve(result.Reference, result.TotalMinor, result.ExpiresAt.AddSeconds(-Math.Max(60, options.Value.SafetySeconds)));
    }
    public async Task<PoolCheckout> CheckoutAsync(CheckoutPoolBookingCommand r, CancellationToken ct)
    {
        await using var lease = await gate.AcquireAsync(r.Id, ct); var b = await Owned(r.Id, r.UserId, ct);
        policy.EnsureAllowed(b.IsMock);
        if (b.OrderId.HasValue) return new(b.Id, b.OrderId.Value, b.OrderNumber!, b.TotalMinor);
        if (!b.IsPayable(DateTimeOffset.UtcNow) || b.Version != r.Version || b.TotalMinor != r.TotalMinor)
            throw new PoolException("رزرو یا مبلغ تغییر کرده است؛ درخواست را بررسی کنید");
        var s = Snapshot(b);
        var created = await mediator.Send(new CreateOrderCommand(b.UserId, "Pools", b.Id,
            [new($"{s.Pool?.Name ?? "بلیط استخر"} - {s.Row.Date} - {s.Row.Time}", b.TotalMinor, 1, 0, b.Id,
                "entertainment.waterpark", ["pools", "provider:" + b.ProviderKey, b.IsMock ? "mock" : "live"],
                JsonSerializer.Serialize(new { bookingId = b.Id, s.Selection, b.IsMock }, Json))],
            OrderKey(b.Id), ReferenceType: "PoolBooking", SagaId: b.Id, PayableUntil: b.PayableUntil), ct);
        b.AttachOrder(created.OrderId, created.OrderNumber); await repository.SaveAsync(ct);
        logger.LogInformation("Pool Order attached. BookingId={BookingId} OrderId={OrderId} Provider={Provider} IsMock={IsMock}", b.Id, b.OrderId, b.ProviderKey, b.IsMock);
        return new(b.Id, created.OrderId, created.OrderNumber, created.FinalAmountMinor);
    }
    public async Task<PoolBookingView> CancelAsync(Guid id, Guid user, CancellationToken ct)
    {
        var b = await Owned(id, user, ct); policy.EnsureAllowed(b.IsMock);
        // Orders acquires the financial lock before the source lock. Never call it while holding our source lock.
        if (!b.OrderId.HasValue)
        {
            await using var lease = await gate.AcquireAsync(id, ct);
            b = await Owned(id, user, ct);
            if (!b.OrderId.HasValue) await CancelProviderAsync(b, ct);
        }
        // Checkout may have attached an Order while cancellation was waiting for the source lock.
        if (b.OrderId.HasValue) await CancelFinancialAsync(b, ct);
        return await GetAsync(id, user, ct);
    }
    public async Task<bool> CancelProviderAsync(PoolBooking b, CancellationToken ct)
    {
        policy.EnsureAllowed(b.IsMock);
        if (b.CancellationStatus == "Cancelled") return true;
        var p = providers.Get(b.ProviderKey);
        if (!p.Capabilities.Cancel || p is not IPoolCancellationProvider cancel || p is not IPoolStatusProvider status || b.ProviderReference == null)
            return false;
        var known = await status.StatusAsync(b.ProviderReference, ct);
        if (known?.Status == "Cancelled") { b.Cancel(); await repository.SaveAsync(ct); return true; }
        if (b.Attempts.Any(x => x.Operation == "cancel" && x.Outcome is "Started" or "Ambiguous") || known == null) return false;
        if (known.Status == "Reserved" && b.Attempts.Any(x => x.Operation == "confirm" && x.Outcome is "Started" or "Ambiguous"))
            return false; // A late confirmation must be settled before cancellation can authorize a refund.
        b.BeginCancellation(); var attempt = b.BeginAttempt("cancel"); await repository.SaveAsync(ct);
        try
        {
            var result = await cancel.CancelAsync(b.ProviderReference, $"cancel:{b.Id:N}", ct);
            if (result.Status != "Cancelled") throw new PoolAmbiguousException();
            attempt.Complete(); b.Cancel();
        }
        catch (PoolException) { attempt.Fail(false); b.Review("لغو توسط تأمین‌کننده پذیرفته نشد؛ نیازمند بررسی است"); }
        catch (Exception ex) when (ex is PoolAmbiguousException or HttpRequestException or OperationCanceledException)
        { attempt.Fail(true); b.Review("نتیجه لغو در حال بررسی است"); }
        await repository.SaveAsync(CancellationToken.None);
        logger.LogInformation("Pool cancellation processed. BookingId={BookingId} OrderId={OrderId} Provider={Provider} Outcome={Outcome}",
            b.Id, b.OrderId, b.ProviderKey, b.CancellationStatus);
        return b.CancellationStatus == "Cancelled";
    }
    public async Task FinalizeAsync(Guid id, CancellationToken ct)
    {
        await using var lease = await gate.AcquireAsync(id, ct);
        var b = await repository.GetAsync(id, ct); if (b?.OrderId is not Guid orderId || b.HasTicket || b.IssuanceStatus == "Failed" || b.CancellationStatus != "None") return;
        policy.EnsureAllowed(b.IsMock);
        var paid = await mediator.Send(new GetOrderByIdQuery(orderId, b.UserId, "User"), ct);
        if (paid?.PaymentState != "Paid" || paid.SourceModule != "Pools" || paid.ReferenceType != "PoolBooking"
            || paid.SourceReferenceId != id || paid.FinalAmountMinor != b.TotalMinor) return;
        if (b.PaymentStatus == "Unpaid") { b.MarkPaid(); await repository.SaveAsync(ct); }
        var p = providers.Get(b.ProviderKey);
        if (p is not IPoolConfirmationProvider confirm || p is not IPoolStatusProvider status || b.ProviderReference == null) return;
        var known = await status.StatusAsync(b.ProviderReference, ct);
        if (known?.Status == "Confirmed" && known.TotalMinor == b.TotalMinor && known.IsMock == b.IsMock)
        {
            if (string.IsNullOrWhiteSpace(known.TicketCode)) b.Review("صدور تأیید شد؛ کد بلیط هنوز دریافت نشده است");
            else b.Issue(known.TicketCode);
            await repository.SaveAsync(ct); return;
        }
        if (b.Attempts.Any(x => x.Operation == "confirm" && x.Outcome is "Started" or "Ambiguous") || known == null) return;
        if (known.Status != "Reserved") { b.FailIssuance(); await repository.SaveAsync(ct); return; }
        var attempt = b.BeginAttempt("confirm"); await repository.SaveAsync(ct);
        try
        {
            var result = await confirm.ConfirmAsync(b.ProviderReference, $"confirm:{b.Id:N}", ct);
            if (result.Status != "Confirmed" || result.TotalMinor != b.TotalMinor || result.IsMock != b.IsMock || string.IsNullOrWhiteSpace(result.TicketCode)) throw new PoolAmbiguousException();
            attempt.Complete(); b.Issue(result.TicketCode);
        }
        catch (PoolException) { attempt.Fail(false); b.FailIssuance(); }
        catch (Exception ex) when (ex is PoolAmbiguousException or HttpRequestException or OperationCanceledException)
        { attempt.Fail(true); b.Review("پرداخت انجام شد؛ نتیجه صدور در حال بررسی است"); }
        await repository.SaveAsync(CancellationToken.None);
        logger.LogInformation("Pool issuance processed. BookingId={BookingId} OrderId={OrderId} Outcome={Outcome}", b.Id, b.OrderId, b.IssuanceStatus);
    }
    public async Task ReconcileAsync(Guid id, CancellationToken ct)
    {
        var b = await repository.GetAsync(id, ct); if (b == null) return; policy.EnsureAllowed(b.IsMock);
        if (b.ReservationStatus == "Reserving")
        {
            await using var lease = await gate.AcquireAsync(id, ct); b = (await repository.GetAsync(id, ct))!;
            var status = providers.Get(b.ProviderKey) as IPoolStatusProvider;
            var known = status == null ? null : await status.FindByOperationAsync(id.ToString("N"), ct);
            if (known?.Status == "Reserved") { ApplyReservation(b, known); await repository.SaveAsync(ct); }
            else if (known?.Status == "Expired") { b.FailReservation(); await repository.SaveAsync(ct); }
            return;
        }
        if (b.OrderId == null && b.ProviderReference != null)
        {
            var old = await mediator.Send(new GetOrderByIdempotencyKeyQuery(OrderKey(id), b.UserId, "Pools"), ct);
            if (old != null)
            {
                await using var lease = await gate.AcquireAsync(id, ct); b = (await repository.GetAsync(id, ct))!;
                b.AttachOrder(old.Id, old.OrderNumber); await repository.SaveAsync(ct);
            }
        }
        if (b.OrderId.HasValue)
        {
            var order = await mediator.Send(new GetOrderByIdQuery(b.OrderId.Value, b.UserId, "User"), ct);
            if (order?.PaymentState == "Paid" && b.CancellationStatus == "None" && b.IssuanceStatus != "Failed") await FinalizeAsync(id, ct);
            b = (await repository.GetAsync(id, ct))!;
            if (order?.PaymentState == "Refunded" || order?.Status == "Cancelled")
            {
                await using var lease = await gate.AcquireAsync(id, ct); b = (await repository.GetAsync(id, ct))!;
                if (order.PaymentState == "Refunded") b.MarkRefunded();
                if (order.Status == "Cancelled") b.MarkOrderCancellationCompleted();
                await repository.SaveAsync(ct); return;
            }
            if (b.IssuanceStatus == "Failed" || b.CancellationStatus is "Pending" or "Cancelled"
                || order?.PaymentState != "Paid" && b.PayableUntil <= DateTimeOffset.UtcNow) await CancelFinancialAsync(b, ct);
        }
        else if (b.ProviderReference != null && b.PayableUntil <= DateTimeOffset.UtcNow)
        {
            await using (var lease = await gate.AcquireAsync(id, ct))
            {
                b = (await repository.GetAsync(id, ct))!;
                if (!b.OrderId.HasValue) await CancelProviderAsync(b, ct);
            }
            if (b.OrderId.HasValue) await CancelFinancialAsync(b, ct);
        }
    }
    private Task<CancelOrderResponse> CancelFinancialAsync(PoolBooking b, CancellationToken ct) => mediator.Send(
        new CancelOrderCommand(b.OrderId!.Value, "لغو یا بازیابی خرید استخر", $"pools-cancel:{b.Id:N}", CallerUserId: b.UserId, CallerRole: "User"), ct);
    public static string OrderKey(Guid id) => $"pools-order:{id:N}";
    private static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
}
