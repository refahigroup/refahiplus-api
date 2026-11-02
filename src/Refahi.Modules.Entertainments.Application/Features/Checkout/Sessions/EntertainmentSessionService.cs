using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MediatR;
using Refahi.Modules.Entertainments.Application.Contracts;
using Refahi.Modules.Entertainments.Application.Contracts.Providers;
using Refahi.Modules.Entertainments.Application.Contracts.Providers.Exceptions;
using Refahi.Modules.Entertainments.Domain;
using Refahi.Modules.Entertainments.Application.Features.Checkout.Prepare;
using Refahi.Modules.Identity.Application.Contracts.Queries;
using Refahi.Modules.Orders.Application.Contracts.Commands;

namespace Refahi.Modules.Entertainments.Application.Features.Checkout.Sessions;

public sealed class EntertainmentSessionService(IEntertainmentSessionRepository sessions, IEntertainmentRepository orders,
    IEntertainmentProviderFactory providers, IEntertainmentSecretProtector secrets, IEntertainmentMutationLock gate, IMediator mediator)
    : IRequestHandler<ReturnEntertainmentSessionToCartCommand, EntertainmentSessionDto>, IRequestHandler<StartEntertainmentSessionCommand, EntertainmentSessionDto>, IRequestHandler<GetEntertainmentSessionQuery, EntertainmentSessionDto>,
      IRequestHandler<ConfirmEntertainmentSessionCommand, PrepareEntertainmentCheckoutResponse>, IRequestHandler<ReleaseEntertainmentSessionCommand>, IRequestHandler<FindEntertainmentSessionQuery, EntertainmentSessionDto?>
{
    public async Task<EntertainmentSessionDto> Handle(StartEntertainmentSessionCommand request, CancellationToken ct)
    {
        await using var held = await gate.AcquireAsync(request.UserId, ct);
        var old = await sessions.FindAsync(request.UserId, request.IdempotencyKey.Trim(), ct);
        var cart = await orders.GetCartAsync(request.UserId, ct);
        var providerKey = request.ProviderKey;
        if (request.CartItemIds is { } ids)
        {
            if (old != null)
            {
                var original = JsonSerializer.Deserialize<EntertainmentReservationRequest>(secrets.Unprotect(old.RequestProtected))!;
                if (!original.Lines.Select(x => x.CartItemId).Order().SequenceEqual(ids.Order())) throw Error("اقلام رزرو با درخواست پیشین مطابقت ندارد");
                providerKey = old.ProviderKey;
            }
            else
            {
                var picked = cart?.Items.Where(x => ids.Contains(x.Id)).ToArray() ?? [];
                if (picked.Length != ids.Count || picked.Select(x => x.ProviderKey).Distinct(StringComparer.OrdinalIgnoreCase).Count() != 1) throw Error("اقلام انتخاب‌شده معتبر نیستند");
                providerKey = picked[0].ProviderKey;
                if (cart!.Items.Count(x => x.ProviderKey.Equals(providerKey, StringComparison.OrdinalIgnoreCase)) != ids.Count) throw Error("اقلام این بخش تغییر کرده‌اند؛ سبد را دوباره بررسی کنید");
            }
        }
        var fingerprint = Hash(JsonSerializer.Serialize(new { Provider = providerKey.Trim().ToLowerInvariant(),
            request.CartVersion, Passengers = request.Passengers.OrderBy(x => x.CartItemId) }));
        if (old != null)
        {
            if (old.Fingerprint != fingerprint) throw Error("کلید یکتایی با اطلاعات دیگری استفاده شده است");
            return Map(old);
        }
        var provider = providers.GetRequired(providerKey);
        if (cart == null) throw Error("سبد خرید خالی است");
        var selected = cart.Items.Where(x => x.ProviderKey.Equals(provider.Key, StringComparison.OrdinalIgnoreCase)).OrderBy(x => x.Id).ToArray();
        if (selected.Length is 0 or > 100) throw Error("تعداد اقلام انتخاب‌شده معتبر نیست");
        if ((provider is IEntertainmentReservationProvider || request.CartVersion != null)
            && request.CartVersion != Features.Cart.Mapper.SelectionVersion(selected))
            throw Error("اقلام یا قیمت سبد تغییر کرده است؛ سبد را دوباره بارگذاری کنید");
        if (request.Passengers.Any(x => selected.All(y => y.Id != x.CartItemId))) throw Error("مسافر به آیتم انتخاب‌شده تعلق ندارد");
        var contact = await mediator.Send(new GetUserContactQuery(request.UserId), ct);
        if (string.IsNullOrWhiteSpace(contact?.FullName) || contact.FullName.Trim().Length < 3
            || new string((contact.MobileNumber ?? "").Where(char.IsDigit).ToArray()).Length is < 10 or > 13)
            throw Error("برای ادامه خرید، نام و شماره موبایل پروفایل خود را تکمیل کنید");
        var lines = selected.Select(x => new EntertainmentSessionLine(x.Id, x.ProgramKey, x.OfferKey, x.PurchaseOptionKey,
            x.Quantity, x.ExpectedUnitPriceMinor, request.Passengers.SingleOrDefault(p => p.CartItemId == x.Id)?.Passengers
                ?? Enumerable.Range(0, x.Quantity).Select(_ => new EntertainmentPassenger("", null)).ToArray())).ToArray();
        var input = new EntertainmentReservationRequest("", contact.FullName.Trim(), contact.MobileNumber!, lines);
        var session = EntertainmentCheckoutSession.Create(request.UserId, provider.Key, request.IdempotencyKey.Trim(), fingerprint,
            secrets.Protect(JsonSerializer.Serialize(input)));
        await sessions.AddAsync(session, ct); await sessions.SaveAsync(ct);
        EntertainmentReservationResult result;
        try
        {
            if (provider is IEntertainmentReservationProvider reservation)
                result = await reservation.ReserveAsync(input with { OperationId = session.Id.ToString("N") }, ct);
            else
            {
                var quoted = new List<EntertainmentReservedLine>();
                foreach (var line in lines)
                    quoted.Add(new(line.CartItemId, await provider.QuoteAsync(new(line.ProgramKey, line.OfferKey, line.PurchaseOptionKey, line.Quantity), ct), line.Passengers));
                result = new("", DateTimeOffset.UtcNow.AddMinutes(30), quoted, "");
            }
        }
        catch (EntertainmentProviderAmbiguousException)
        {
            session.Unknown(); await sessions.SaveAsync(CancellationToken.None); throw;
        }
        catch
        {
            // Validation and read-only provider failures happen before a reservation can be created.
            // Keep the idempotency record, but allow the UI to start a fresh session.
            session.Released(); await sessions.SaveAsync(CancellationToken.None); throw;
        }
        // A failure to persist a successful reservation must not mark the external reservation as released.
        session.Reserved(result.Reference, secrets.Protect(JsonSerializer.Serialize(result)), result.PayableUntil);
        await sessions.SaveAsync(ct);
        return Map(session);
    }

    public async Task<EntertainmentSessionDto> Handle(GetEntertainmentSessionQuery request, CancellationToken ct)
    {
        await using var userLock = await gate.AcquireAsync(request.UserId, ct);
        await using var sessionLock = await gate.AcquireAsync(request.SessionId, ct);
        var session = await sessions.GetFreshAsync(request.SessionId, ct) ?? throw Error("رزرو یافت نشد");
        if (session.UserId != request.UserId) throw new UnauthorizedAccessException("دسترسی غیرمجاز است");
        if (session.EntertainmentPurchaseId == null && session.CreatedAt < DateTimeOffset.UtcNow.AddMinutes(-2)
            && session.Status is EntertainmentSessionStatus.Unknown or EntertainmentSessionStatus.Reserving
            && providers.GetRequired(session.ProviderKey) is IEntertainmentReservationRecoveryProvider recovery)
        {
            if (await recovery.TryReleaseUnpaidReservationAsync(session.Id.ToString("N"), ct))
            {
                session.ConfirmRecoveredRelease(secrets.Protect("Provider confirmed unpaid reservation release; receipt release:reserve:" + session.Id.ToString("N")));
                await sessions.SaveAsync(ct);
            }
        }
        return Map(session);
    }
    public async Task<EntertainmentSessionDto?> Handle(FindEntertainmentSessionQuery request, CancellationToken ct)
    {
        var session = await sessions.FindAsync(request.UserId, request.IdempotencyKey, ct);
        return session == null ? null : Map(session);
    }

    public async Task<PrepareEntertainmentCheckoutResponse> Handle(ConfirmEntertainmentSessionCommand request, CancellationToken ct)
    {
        await using var userLock = await gate.AcquireAsync(request.UserId, ct);
        await using var sessionLock = await gate.AcquireAsync(request.SessionId, ct);
        var session = await Owned(request.UserId, request.SessionId, ct);
        if (session.Status == EntertainmentSessionStatus.Released) throw Error("این رزرو آزاد شده است؛ خرید را از سبد ادامه دهید");
        if (session.EntertainmentPurchaseId.HasValue)
        {
            var replay = await orders.GetOrderAsync(session.EntertainmentPurchaseId.Value, ct) ?? throw Error("سفارش متصل یافت نشد");
            return await AttachOrder(replay, ct);
        }
        if (Version(session) != request.Version) throw Error("نسخه رزرو تغییر کرده است؛ دوباره بررسی کنید");
        if (session.Status != EntertainmentSessionStatus.Reserved || session.PayableUntil <= DateTimeOffset.UtcNow) throw Error("رزرو قابل پرداخت نیست یا مهلت آن تمام شده است");
        var reservation = ReadReservation(session);
        var input = JsonSerializer.Deserialize<EntertainmentReservationRequest>(secrets.Unprotect(session.RequestProtected))!;
        var snapshots = reservation.Lines.Select(x => new EntertainmentPurchaseItemSnapshot(x.Quote.ProviderKey, x.Quote.SupplierKey,
            x.Quote.ProgramKey, x.Quote.OfferKey, x.Quote.PurchaseOptionKey, x.Quote.ProgramTitle + " - " + x.Quote.OptionTitle,
            x.Quote.OfferTitle, x.Quote.CategoryCode, x.Quote.Quantity, x.Quote.UnitPriceMinor, x.Quote.ProviderPayloadJson)).ToArray();
        if (snapshots.Select(x => x.ProviderKey).Distinct(StringComparer.OrdinalIgnoreCase).Count() != 1) throw Error("هر پرداخت باید متعلق به یک پرووایدر باشد");
        var value = EntertainmentPurchase.Create(request.UserId, "session:" + session.Id.ToString("N"),
            Hash(JsonSerializer.Serialize(reservation.Lines)), secrets.Protect(input.RecipientName), secrets.Protect(input.RecipientMobile), snapshots);
        value.AttachSession(session.Id, reservation.PayableUntil, reservation.Reference, reservation.ProtectedContext, session.RequestProtected);
        await orders.AddOrderAsync(value, ct);
        session.AttachOrder(value.Id);
        // The session and EntertainmentPurchase share one DbContext and are committed before calling Orders.
        await sessions.SaveAsync(ct);
        return await AttachOrder(value, ct);
    }

    public async Task<EntertainmentSessionDto> Handle(ReturnEntertainmentSessionToCartCommand request, CancellationToken ct)
    {
        await using var userLock = await gate.AcquireAsync(request.UserId, ct);
        await using var sessionLock = await gate.AcquireAsync(request.SessionId, ct);
        var session = await sessions.GetFreshAsync(request.SessionId, ct) ?? throw Error("رزرو یافت نشد");
        if (session.UserId != request.UserId) throw new UnauthorizedAccessException("دسترسی غیرمجاز است");
        if (session.Status == EntertainmentSessionStatus.Released) return Map(session);
        if (!session.EntertainmentPurchaseId.HasValue) throw Error("رزرو هنوز به سفارش متصل نشده است");
        var purchase = await orders.GetOrderAsync(session.EntertainmentPurchaseId.Value, ct) ?? throw Error("خرید یافت نشد");
        if (!purchase.OrderId.HasValue) throw Error("سفارش متصل یافت نشد");
        var reservation = ReadReservation(session);
        // Orders checks Unpaid under its payment lock before invoking provider cancellation.
        await mediator.Send(new CancelOrderCommand(purchase.OrderId.Value, "بازگشت به سبد برای ویرایش خرید",
            "entertainments-return:" + session.Id.ToString("N"), CallerUserId: request.UserId, CallerRole: "User", UnpaidOnly: true), ct);
        // Cancellation participants refresh the shared DbContext and detach tracked sessions.
        session = await sessions.GetFreshAsync(request.SessionId, ct) ?? throw Error("رزرو یافت نشد");
        var cart = await orders.GetCartAsync(request.UserId, ct);
        if (cart == null) { cart = EntertainmentCart.Create(request.UserId); await orders.AddCartAsync(cart, ct); }
        foreach (var line in reservation.Lines)
        {
            var q = line.Quote;
            if (cart.Items.Any(x => x.ProviderKey.Equals(q.ProviderKey, StringComparison.OrdinalIgnoreCase)
                && x.SupplierKey.Equals(q.SupplierKey, StringComparison.OrdinalIgnoreCase) && x.ProgramKey == q.ProgramKey
                && x.OfferKey == q.OfferKey && x.PurchaseOptionKey.Equals(q.PurchaseOptionKey, StringComparison.OrdinalIgnoreCase))) continue;
            cart.AddOrReplace(new(q.ProviderKey, q.SupplierKey, q.ProgramKey, q.OfferKey, q.PurchaseOptionKey,
                q.ProgramTitle + " - " + q.OptionTitle, q.SupplierTitle, q.ProgramTitle, q.ProgramImageUrl,
                q.OptionTitle, q.OfferTitle, q.Quantity, q.UnitPriceMinor, q.OriginalUnitPriceMinor));
        }
        session.Released();
        // One module DbContext commits restoration and the replay marker together.
        await sessions.SaveAsync(ct);
        return Map(session);
    }

    private async Task<PrepareEntertainmentCheckoutResponse> AttachOrder(EntertainmentPurchase value, CancellationToken ct)
    {
        if (!value.OrderId.HasValue)
        {
            var created = await mediator.Send(new CreateOrderCommand(value.UserId, "Entertainments", value.Id,
                value.Items.Select(x => new CreateOrderItemInput(x.Title + " - " + x.OfferTitle, x.UnitPriceMinor, x.Quantity, 0,
                    x.Id, x.CategoryCode, ["entertainments"], JsonSerializer.Serialize(new
                    { entertainments_order_id = value.Id, x.ProgramKey, x.OfferKey, x.PurchaseOptionKey }))).ToList(),
                "entertainments-order:" + value.Id.ToString("N"), ReferenceType: "EntertainmentPurchase", PayableUntil: value.PayableUntil), ct);
            value.AttachOrder(created.OrderId);
            var cart = await orders.GetCartAsync(value.UserId, ct);
            if (cart != null && value.CartSelectionProtected != null)
            {
                var selection = JsonSerializer.Deserialize<EntertainmentReservationRequest>(secrets.Unprotect(value.CartSelectionProtected))!;
                foreach (var line in selection.Lines)
                {
                    var current = cart.Items.SingleOrDefault(x => x.Id == line.CartItemId);
                    if (current != null && current.ProgramKey == line.ProgramKey && current.OfferKey == line.OfferKey
                        && current.PurchaseOptionKey == line.PurchaseOptionKey && current.Quantity == line.Quantity
                        && current.ExpectedUnitPriceMinor == line.ExpectedUnitPriceMinor) cart.Remove(current.Id);
                }
            }
            await orders.SaveChangesAsync(ct);
        }
        var order = await mediator.Send(new Orders.Application.Contracts.Queries.GetOrderByIdQuery(value.OrderId!.Value, value.UserId, "User"), ct)
            ?? throw Error("سفارش متصل یافت نشد");
        return new(value.Id, order.Id, order.OrderNumber, order.FinalAmountMinor, $"/checkout/orders/{order.Id}");
    }

    public async Task<Unit> Handle(ReleaseEntertainmentSessionCommand request, CancellationToken ct)
    {
        await using var held = await gate.AcquireAsync(request.SessionId, ct);
        var session = await Owned(request.UserId, request.SessionId, ct);
        if (session.Status == EntertainmentSessionStatus.Released) return Unit.Value;
        if (session.Status is EntertainmentSessionStatus.Reserving or EntertainmentSessionStatus.Unknown)
            throw Error("نتیجه رزرو نیازمند بررسی است");
        if (session.Status != EntertainmentSessionStatus.Releasing)
        { session.Releasing(); await sessions.SaveAsync(ct); }
        if (providers.GetRequired(session.ProviderKey) is IEntertainmentReservationProvider reservation && !string.IsNullOrWhiteSpace(session.ReservationReference))
            await reservation.ReleaseAsync(session.ReservationReference, ct);
        session.Released(); await sessions.SaveAsync(ct); return Unit.Value;
    }
    private async Task<EntertainmentCheckoutSession> Owned(Guid user, Guid id, CancellationToken ct)
    {
        var session = await sessions.GetAsync(id, ct) ?? throw Error("رزرو یافت نشد");
        if (session.UserId != user) throw new UnauthorizedAccessException("دسترسی به این رزرو مجاز نیست");
        return session;
    }
    private EntertainmentReservationResult ReadReservation(EntertainmentCheckoutSession session) =>
        JsonSerializer.Deserialize<EntertainmentReservationResult>(secrets.Unprotect(session.ReservationProtected!))!;
    private EntertainmentSessionDto Map(EntertainmentCheckoutSession s)
    {
        IReadOnlyList<EntertainmentReservedLine> lines = s.ReservationProtected == null ? [] : ReadReservation(s).Lines;
        return new(s.Id, s.ProviderKey, s.Status == EntertainmentSessionStatus.Reserved && s.PayableUntil <= DateTimeOffset.UtcNow ? "Expired" : s.Status.ToString(),
            Version(s), s.PayableUntil, lines.Sum(x => checked(x.Quote.UnitPriceMinor * x.Quote.Quantity)),
            lines.Select(x => new EntertainmentSessionItemDto(x.CartItemId, x.Quote.ProgramTitle, x.Quote.OptionTitle, x.Quote.Quantity, x.Quote.UnitPriceMinor)).ToArray(), s.EntertainmentPurchaseId);
    }
    private static string Version(EntertainmentCheckoutSession s) => Hash(s.ReservationProtected ?? s.Fingerprint);
    private static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    private static EntertainmentDomainException Error(string message) => new(message, "CHECKOUT_SESSION_CONFLICT");
}
