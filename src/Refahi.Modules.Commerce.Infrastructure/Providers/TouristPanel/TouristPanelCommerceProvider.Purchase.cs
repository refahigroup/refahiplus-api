using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Refahi.Modules.Commerce.Application.Contracts.Providers;
using Refahi.Modules.Commerce.Application.Contracts.Providers.Exceptions;
using Refahi.Modules.Commerce.Application.Contracts.Providers.Requests;
using Refahi.Modules.Commerce.Domain;
using Refahi.Modules.Commerce.Infrastructure.Providers.TouristPanel.Contracts;

namespace Refahi.Modules.Commerce.Infrastructure.Providers.TouristPanel;

public sealed partial class TouristPanelCommerceProvider
{
    public async Task<CommerceReservationResult> ReserveAsync(CommerceReservationRequest request, CancellationToken ct)
    {
        if (!O.IsSaleConfigured) throw Error("فروش توریست‌پنل فعال نیست");
        var groups = new List<TpAddTempGroupDto>();
        var lines = new List<CommerceReservedLine>();
        foreach (var line in request.Lines)
        {
            var s = await Select(new(line.ProductKey, line.OfferKey, line.PurchaseOptionKey, line.Quantity), ct);
            if (line.Passengers.Count != line.Quantity) throw Error("تعداد مشخصات مسافران با بلیط‌ها یکسان نیست");
            if (line.Passengers.Any(x => (s.Quote.RequiresManifest || s.Quote.RequiresIdentityNumber) && string.IsNullOrWhiteSpace(x.Name)
                || s.Quote.RequiresIdentityNumber && string.IsNullOrWhiteSpace(x.IdentityNumber)))
                throw Error("مشخصات مسافران کامل نیست");
            groups.Add(new TpAddTempGroupDto
            {
                ProgramId = s.Program.Id, SupplyChainHojreId = s.Program.SupplyChainHojreId,
                EventId = (int)s.Program.Type == 2 ? line.OfferKey : null,
                Tickets = line.Passengers.Select(p => new TpAddTempTicketDto
                {
                    ProgramTicketTypeId = s.Type.ProgramTicketTypeId, EventTicketTypeId = s.Type.EventTicketTypeId,
                    ReferenceTicketTypeId = s.Type.ReferenceTicketTypeId, IsNumberEqualsReferenceTicketType = s.Type.IsNumberEqualsReferenceTicketType,
                    CommissionPriceCategoryId = s.Price.CommissionPriceCategory!.CommissionPriceCategoryId,
                    ManifestUniqueName = s.Quote.RequiresManifest || s.Quote.RequiresIdentityNumber ? p.Name.Trim() : null,
                    ManifestUniqueNumber = s.Quote.RequiresIdentityNumber ? p.IdentityNumber?.Trim() : null
                }).ToList()
            });
            lines.Add(new(line.CartItemId, s.Quote, line.Passengers));
        }
        if (groups.Count == 0) throw Error("سبد انتخاب‌شده خالی است");
        var operation = "reserve:" + request.OperationId;
        if (await Receipt(operation, ct) is not null) throw new CommerceProviderAmbiguousException("تلاش رزرو قبلی باید بررسی شود");
        await SaveReceipt(operation, new ReservationCheckpoint(groups, null), ct);
        var cart = groups.Count == 1 ? await client.AddGroupAsync(groups[0], null, null, ct) : await client.AddGroupsAsync(groups, null, ct);
        await SaveReceipt(operation, new ReservationCheckpoint(groups, cart), ct);
        if (!Guid.TryParse(cart.Id, out var id) || id == Guid.Empty) throw new CommerceProviderAmbiguousException("شناسه رزرو برگشتی معتبر نیست");
        cart = await client.GetCartAsync(cart.Id!, ct);
        var context = new ReservationContext(O.AccountKey, lines, cart);
        ValidateCart(context, cart);
        var deadlines = new[] { ParseDate(cart.ExpireDateTime) }.Concat((cart.Groups ?? []).Select(x => ParseDate(x.ExpireTime))).ToArray();
        if (deadlines.Any(x => !x.HasValue)) throw new CommerceProviderAmbiguousException("زمان انقضای رزرو قابل تفسیر نیست");
        var until = deadlines.Min()!.Value.AddSeconds(-O.ReservationSafetySeconds);
        if (until <= DateTimeOffset.UtcNow) throw Error("مهلت رزرو به پایان رسیده است");
        return new(cart.Id!, until, lines, secrets.Protect(JsonSerializer.Serialize(context, TouristPanelClient.Json)));
    }

    public async Task ReleaseAsync(string reference, CancellationToken ct)
    {
        if (!await client.DeleteCartAsync(reference, ct))
            throw new CommerceProviderAmbiguousException("آزادسازی رزرو توریست‌پنل تأیید نشد");
    }

    public async Task<CommerceFulfillmentResult> FulfillAsync(CommerceFulfillmentRequest request, CancellationToken ct)
    {
        var old = await Receipt(request.OperationId, ct);
        if (old != null) return await GetStatusAsync(request.OperationId, null, ct);
        if (request.ReservationContext is null || request.ReservationReference is null)
            throw Error("رزرو توریست‌پنل به سفارش متصل نیست");
        var context = JsonSerializer.Deserialize<ReservationContext>(secrets.Unprotect(request.ReservationContext), TouristPanelClient.Json)
            ?? throw Error("اطلاعات رزرو معتبر نیست");
        if (context.AccountKey != O.AccountKey || context.Cart.Id != request.ReservationReference)
            throw Error("حساب یا شناسه رزرو تغییر کرده است");
        // SalesEnabled controls new reservations, not fulfillment/recovery of already-paid orders.
        if (!O.SettlementConfirmed || !O.PaymentMethod.HasValue || !O.BankGateway.HasValue)
            return new("", []) { Status = "ManualReview" };
        var cart = await client.GetCartAsync(request.ReservationReference, ct);
        ValidateCart(context, cart);
        if (new[] { ParseDate(cart.ExpireDateTime) }.Concat((cart.Groups ?? []).Select(x => ParseDate(x.ExpireTime)))
            .Any(x => !x.HasValue || x <= DateTimeOffset.UtcNow))
            return new("", []) { Status = "Failed" };
        var checkpoint = new PurchaseCheckpoint(context, null);
        await SaveReceipt(request.OperationId, checkpoint, ct);
        TpPurchaseResultDto result;
        try
        {
            result = await client.FinalizeAsync(cart.Id!, new TpFinalizingTempShoppingCartDto
            {
                PaymentMethod = (TpPaymentMethod)O.PaymentMethod.Value, BankGateway = (TpPaymentGatewayType)O.BankGateway.Value,
                CustomerFullName = request.RecipientName, CustomerMobile = request.RecipientMobile,
                InternalTrackingCode = request.OperationId, TotalDiscount = 0, SendTicketSms = false
            }, O.CashDeskId, null, ct);
        }
        catch (TouristPanelHttpException ex) when (ex.StatusCode is 401 or 403)
        {
            // A response to an authenticated mutation was rejected. Keep the evidence for recovery.
            await SaveReceipt(request.OperationId, checkpoint with { Rejected = true }, ct);
            return new("", []) { Status = "Failed" };
        }
        await SaveReceipt(request.OperationId, checkpoint with { Result = result }, ct);
        return MapPurchase(result, context, request.OperationId);
    }

    public async Task<CommerceFulfillmentResult> GetStatusAsync(string operationId, string? invoiceId, CancellationToken ct)
    {
        var receipt = await Receipt(operationId, ct);
        if (receipt == null) return new("", []) { Status = "ManualReview" };
        var checkpoint = JsonSerializer.Deserialize<PurchaseCheckpoint>(secrets.Unprotect(receipt.PayloadProtected), TouristPanelClient.Json)
            ?? throw Error("سابقه خرید قابل خواندن نیست");
        if (checkpoint.Rejected) return new("", []) { Status = "Failed" };
        // The invoice is recovered only from our persisted provider response, never from caller input.
        var actualInvoice = checkpoint.Result?.TicketsInvoice?.Id;
        if (string.IsNullOrWhiteSpace(actualInvoice)) return new("", []) { Status = "ManualReview" };
        if (invoiceId != null && !string.Equals(actualInvoice, invoiceId, StringComparison.OrdinalIgnoreCase))
            throw Error("شناسه فاکتور با سابقه خرید متفاوت است");
        var result = await client.GetPurchaseAsync(actualInvoice, ct);
        if (result.TicketsInvoice?.Id != actualInvoice) return new(actualInvoice, []) { Status = "ManualReview", ProviderInvoiceId = actualInvoice };
        await SaveReceipt(operationId, checkpoint with { Result = result }, ct);
        return MapPurchase(result, checkpoint.Context, operationId);
    }

    private static void ValidateCart(ReservationContext context, TpTempShoppingCartDto cart)
    {
        if (context.Cart.Id != cart.Id || cart.Groups is null || cart.Groups.Count != context.Lines.Count)
            throw new CommerceProviderAmbiguousException("گروه‌های رزرو با سفارش متفاوت است");
        var unmatched = cart.Groups.ToList();
        foreach (var line in context.Lines)
        {
            using var payload = JsonDocument.Parse(line.Quote.ProviderPayloadJson);
            var p = payload.RootElement;
            var category = p.GetProperty("CommissionPriceCategory").GetProperty("commissionPriceCategoryId").GetString();
            var g = unmatched.SingleOrDefault(x => x.ProgramId == line.Quote.ProductKey && x.SupplyChainHojreId == line.Quote.SellerKey
                && x.EventId == (line.Quote.OfferKey.StartsWith("service:") ? null : line.Quote.OfferKey)
                && x.ProgramTicketTypeId == p.GetProperty("ProgramTicketTypeId").GetString()
                && x.EventTicketTypeId == p.GetProperty("EventTicketTypeId").GetString()
                && x.CommissionPriceCategoryId == category);
            if (g?.Tickets == null || g.Tickets.Count != line.Quote.Quantity || g.TicketsCount != line.Quote.Quantity
                || g.Tickets.Any(x => x.BuyPrice != line.Quote.ProviderCostMinor)
                || g.TotalBuyPrice != line.Quote.ProviderCostMinor * line.Quote.Quantity
                || g.Tickets.Select(x => x.TicketId).Distinct().Count() != line.Quote.Quantity)
                throw new CommerceProviderAmbiguousException("تعداد یا بهای رزرو با انتخاب متفاوت است");
            if (line.Quote.RequiresManifest || line.Quote.RequiresIdentityNumber)
            {
                var actual = g.Tickets.Select(x => (x.ManifestUniqueName ?? "").Trim() + "|" + (line.Quote.RequiresIdentityNumber ? x.ManifestUniqueNumber?.Trim() : "")).Order().ToArray();
                var expected = line.Passengers.Select(x => x.Name.Trim() + "|" + (line.Quote.RequiresIdentityNumber ? x.IdentityNumber?.Trim() : "")).Order().ToArray();
                if (!actual.SequenceEqual(expected)) throw new CommerceProviderAmbiguousException("مشخصات بلیط‌های رزرو تطبیق ندارد");
            }
            unmatched.Remove(g);
        }
        if (cart.Groups.Sum(x => x.TotalTicketPrice) != cart.TotalAmount || cart.DiscountAmount != 0)
            throw new CommerceProviderAmbiguousException("جمع مالی سبد رزرو قابل تطبیق نیست");
    }

    private CommerceFulfillmentResult MapPurchase(TpPurchaseResultDto result, ReservationContext context, string operationId)
    {
        var invoice = result.TicketsInvoice;
        var id = invoice?.Id;
        CommerceFulfillmentResult Pending(string status) => new(id ?? "", [])
        { Status = status, ProviderInvoiceId = id, ProviderPaymentId = result.PaymentId };
        if (result.PurchaseStatus == TpPurchaseStatus.IPGRedirect) return Pending("ManualReview");
        if (!result.Status || result.TempReserveIsExpired) return Pending("ManualReview");
        if (invoice == null || !Guid.TryParse(id, out _)) return Pending("ManualReview");
        if (!string.IsNullOrWhiteSpace(invoice.InternalTrackingCode)
            && !invoice.InternalTrackingCode.Equals(operationId, StringComparison.Ordinal)) return Pending("ManualReview");
        if (invoice.PurchaseStatus != result.PurchaseStatus) return Pending("ManualReview");
        if (result.PurchaseStatus == TpPurchaseStatus.PaymentWaiting) return Pending("Pending");
        if (result.PurchaseStatus != TpPurchaseStatus.Payed) return Pending("ManualReview");
        var groups = invoice.TicketGroups ?? [];
        var tickets = groups.SelectMany(x => x.Tickets ?? []).ToArray();
        if (tickets.Length != context.Lines.Sum(x => x.Quote.Quantity) || tickets.Any(x => string.IsNullOrWhiteSpace(x.Id))
            || tickets.Select(x => x.Id).Distinct().Count() != tickets.Length) return Pending("Pending");
        if (invoice.TotalSellerBuyAmount != context.Lines.Sum(x => x.Quote.ProviderCostMinor * x.Quote.Quantity)
            || tickets.Any(x => x.IsCanceled || x.IsRevoked)) return Pending("ManualReview");
        foreach (var line in context.Lines)
        {
            using var payload = JsonDocument.Parse(line.Quote.ProviderPayloadJson);
            var type = payload.RootElement.GetProperty("ProgramTicketTypeId").GetString();
            var eventType = payload.RootElement.GetProperty("EventTicketTypeId").GetString();
            var category = payload.RootElement.GetProperty("CommissionPriceCategory").GetProperty("commissionPriceCategoryId").GetString();
            var matching = groups.Where(g => g.ProgramId == line.Quote.ProductKey && g.SupplyChainHojreId == line.Quote.SellerKey
                && g.ProgramTicketTypeId == type && g.EventTicketTypeId == eventType
                && g.EventId == (line.Quote.OfferKey.StartsWith("service:") ? null : line.Quote.OfferKey))
                .SelectMany(g => g.Tickets ?? []).Where(t => t.CommissionPriceCategoryId == category).ToArray();
            if (matching.Length != line.Quote.Quantity || matching.Any(t => t.SellerBuyAmount != line.Quote.ProviderCostMinor)) return Pending("ManualReview");
            if (line.Quote.RequiresManifest || line.Quote.RequiresIdentityNumber)
            {
                var actual = matching.Select(t => (t.ManifestUniqueName ?? "").Trim() + "|" + (line.Quote.RequiresIdentityNumber ? t.ManifestUniqueNumber?.Trim() : "")).Order();
                var expected = line.Passengers.Select(p => p.Name.Trim() + "|" + (line.Quote.RequiresIdentityNumber ? p.IdentityNumber?.Trim() : "")).Order();
                if (!actual.SequenceEqual(expected)) return Pending("ManualReview");
            }
        }
        var documents = new List<CommerceDeliveryDocument>();
        void Add(string title, string? url)
        {
            if (Uri.TryCreate(url, UriKind.Absolute, out var uri) && uri.Scheme == "https"
                && O.DeliveryHosts.Contains(uri.Host, StringComparer.OrdinalIgnoreCase)) documents.Add(new(title, uri.ToString()));
        }
        Add("دریافت بلیط‌ها", result.TicketsUrl); Add("دریافت فاکتور", result.InvoiceUrl); Add("نمایش بلیط‌ها", result.PublicTicketsUrl);
        foreach (var group in result.TicketTypeGroupUrls ?? []) Add(group.ProgramTitle + " - " + group.TicketType, group.Url);
        if (documents.Count == 0 && tickets.Any(t => string.IsNullOrWhiteSpace(t.TicketNo) && string.IsNullOrWhiteSpace(t.QrCode))) return Pending("Pending");
        return new(id!, groups.SelectMany(g => (g.Tickets ?? []).Select(t => new CommerceIssuedTicket(t.TicketNo ?? t.Id!, false)
        { ProviderTicketId = t.Id, Title = g.ProgramTicketTypeTitle ?? g.TicketTypeTitle, QrCode = t.QrCode, GroupId = g.GroupId })).ToArray())
        { ProviderInvoiceId = id, ProviderPaymentId = result.PaymentId, Documents = documents };
    }
    private Task<CommerceProviderReceipt?> Receipt(string operation, CancellationToken ct) => db.ProviderReceipts.SingleOrDefaultAsync(x => x.OperationId == operation && x.AccountKey == O.AccountKey, ct);
    private async Task SaveReceipt<T>(string operation, T payload, CancellationToken ct)
    {
        var receipt = await Receipt(operation, ct);
        var json = secrets.Protect(JsonSerializer.Serialize(payload, TouristPanelClient.Json));
        if (receipt is null) db.ProviderReceipts.Add(CommerceProviderReceipt.Create(operation, O.AccountKey, json));
        else receipt.Update(json);
        await db.SaveChangesAsync(ct);
    }
    private sealed record ReservationCheckpoint(IReadOnlyList<TpAddTempGroupDto> Groups, TpTempShoppingCartDto? Cart);
    private sealed record PurchaseCheckpoint(ReservationContext Context, TpPurchaseResultDto? Result)
    { public bool Rejected { get; init; } }
}
