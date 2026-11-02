using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using Refahi.Modules.Entertainments.Application.Contracts.Providers;
using Refahi.Modules.Entertainments.Application.Contracts.Providers.Exceptions;
using Refahi.Modules.Entertainments.Application.Contracts.Providers.Requests;
using Refahi.Modules.Entertainments.Domain;
using Refahi.Modules.Entertainments.Infrastructure.Providers.TouristPanel.Contracts;

namespace Refahi.Modules.Entertainments.Infrastructure.Providers.TouristPanel;

public sealed partial class TouristPanelProvider
{
    public async Task<EntertainmentReservationResult> ReserveAsync(EntertainmentReservationRequest request, CancellationToken ct)
    {
        if (!O.IsSaleConfigured) throw Error("فروش توریست‌پنل فعال نیست");
        var groups = new List<TpAddTempGroupDto>();
        var lines = new List<EntertainmentReservedLine>();
        var selections = new List<(EntertainmentSessionLine Line, Selection Selected)>();
        foreach (var input in request.Lines)
        {
            if (input.Passengers.Count != input.Quantity) throw Error("تعداد مشخصات مسافران با بلیط‌ها یکسان نیست");
            var selected = await Select(new(input.ProgramKey, input.OfferKey, input.PurchaseOptionKey, input.Quantity), ct);
            if (selected.Quote.UnitPriceMinor != input.ExpectedUnitPriceMinor)
                logger?.LogInformation("TouristPanel reservation repriced Operation={Operation} Program={Program} ExpectedUnitPrice={ExpectedUnitPrice} UnitPrice={UnitPrice} PricingVersion={PricingVersion}",
                    request.OperationId, input.ProgramKey, input.ExpectedUnitPriceMinor, selected.Quote.UnitPriceMinor, selected.Quote.PricingVersion);
            selections.Add((input, selected));
        }
        // Legacy carts may contain the same ticket type under different old price-category hashes.
        foreach (var selection in selections.GroupBy(x => (x.Selected.Quote.ProgramKey, x.Selected.Quote.OfferKey, x.Selected.Quote.PurchaseOptionKey)))
        {
            var first = selection.First();
            var quantity = selection.Sum(x => x.Line.Quantity);
            if (quantity is < 1 or > 100 || selection.Any(x => x.Selected.Quote.ProviderPayloadJson != first.Selected.Quote.ProviderPayloadJson
                || (int)x.Selected.Program.Type == 2 && x.Selected.Type.RemainingCapacity < quantity))
                throw Error("قیمت، تعداد یا ظرفیت اقلام تکراری تغییر کرده است؛ سبد را دوباره بررسی کنید");
            var line = first.Line with { Quantity = quantity, Passengers = selection.SelectMany(x => x.Line.Passengers).ToArray() };
            var s = first.Selected with { Quote = first.Selected.Quote with { Quantity = quantity } };
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
        try
        {
            _ = lines.Sum(x => checked(x.Quote.UnitPriceMinor * x.Quote.Quantity));
            _ = lines.Sum(x => checked(x.Quote.ProviderCostMinor * x.Quote.Quantity));
        }
        catch (OverflowException) { throw Error("مبلغ کل رزرو معتبر نیست"); }
        var operation = "reserve:" + request.OperationId;
        if (await Receipt(operation, ct) is not null) throw new EntertainmentProviderAmbiguousException("تلاش رزرو قبلی باید بررسی شود");
        await SaveReceipt(operation, new ReservationCheckpoint(groups, null), ct);
        try
        {
            var cart = groups.Count == 1 ? await client.AddGroupAsync(groups[0], null, null, ct) : await client.AddGroupsAsync(groups, null, ct);
            await SaveReceipt(operation, new ReservationCheckpoint(groups, cart), ct);
            if (!Guid.TryParse(cart.Id, out var id) || id == Guid.Empty) throw new EntertainmentProviderAmbiguousException("شناسه رزرو برگشتی معتبر نیست");
            cart = await client.GetCartAsync(cart.Id!, ct);
            var context = new ReservationContext(O.AccountKey, lines, cart);
            context = ValidateCart(context, cart, captureRetailCost: true);
            var deadlines = ReservationDeadlines(cart);
            if (deadlines.Any(x => !x.HasValue)) throw new EntertainmentProviderAmbiguousException("زمان انقضای رزرو قابل تفسیر نیست");
            var until = deadlines.Min()!.Value.AddSeconds(-O.ReservationSafetySeconds);
            if (until <= DateTimeOffset.UtcNow) throw Error("مهلت رزرو به پایان رسیده است");
            return new(cart.Id!, until, context.Lines, secrets.Protect(JsonSerializer.Serialize(context, TouristPanelClient.Json)));
        }
        catch (EntertainmentProviderAmbiguousException) { throw; }
        catch (Exception ex)
        {
            throw new EntertainmentProviderAmbiguousException("نتیجه رزرو پس از ارسال نیازمند بررسی است", ex);
        }
    }

    public async Task ReleaseAsync(string reference, CancellationToken ct)
    {
        if (!await client.DeleteCartAsync(reference, ct))
            throw new EntertainmentProviderAmbiguousException("آزادسازی رزرو توریست‌پنل تأیید نشد");
    }

    public async Task<bool> TryReleaseUnpaidReservationAsync(string operationId, CancellationToken ct)
    {
        var recoveryKey = "release:reserve:" + operationId;
        var confirmed = await Receipt(recoveryKey, ct);
        if (confirmed != null)
            return JsonSerializer.Deserialize<bool>(secrets.Unprotect(confirmed.PayloadProtected), TouristPanelClient.Json);
        var receipt = await Receipt("reserve:" + operationId, ct);
        if (receipt == null || string.IsNullOrEmpty(receipt.PayloadProtected)) return false;
        var checkpoint = JsonSerializer.Deserialize<ReservationCheckpoint>(secrets.Unprotect(receipt.PayloadProtected), TouristPanelClient.Json);
        if (!Guid.TryParse(checkpoint?.Cart?.Id, out var id) || id == Guid.Empty) return false;
        var cart = await client.GetCartAsync(id.ToString(), ct);
        if (!Guid.TryParse(cart.Id, out var actual) || actual != id
            || cart.PaymentId != null || cart.PurchaseStatus != TpPurchaseStatus.PaymentWaiting) return false;
        if (!await client.DeleteCartAsync(id.ToString(), ct)) return false;
        await SaveReceipt(recoveryKey, true, ct);
        logger?.LogInformation("TouristPanel unpaid reservation released Operation={Operation}", operationId);
        return true;
    }

    public async Task<EntertainmentFulfillmentResult> FulfillAsync(EntertainmentFulfillmentRequest request, CancellationToken ct)
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
        if (ReservationDeadlines(cart).Any(x => !x.HasValue || x <= DateTimeOffset.UtcNow))
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

    public async Task<EntertainmentFulfillmentResult> GetStatusAsync(string operationId, string? invoiceId, CancellationToken ct)
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

    private DateTimeOffset?[] ReservationDeadlines(TpTempShoppingCartDto cart) =>
        new[] { ParseDate(cart.ExpireDateTime) }.Concat((cart.Groups ?? [])
            // Observed TouristPanel unset group expiry: .NET MinValue with Tehran's historical offset.
            .Where(x => x.ExpireTime != "0001-01-01T00:00:00+03:26")
            .Select(x => ParseDate(x.ExpireTime))).ToArray();

    private ReservationContext ValidateCart(ReservationContext context, TpTempShoppingCartDto cart, bool captureRetailCost = false)
    {
        if (context.Cart.Id != cart.Id || cart.Groups is null || cart.Groups.Count != context.Lines.Count)
            throw new EntertainmentProviderAmbiguousException("گروه‌های رزرو با سفارش متفاوت است");
        var unmatched = cart.Groups.ToList();
        var reservedLines = new List<EntertainmentReservedLine>();
        foreach (var line in context.Lines)
        {
            using var payload = JsonDocument.Parse(line.Quote.ProviderPayloadJson);
            var p = payload.RootElement;
            var category = p.GetProperty("CommissionPriceCategory").GetProperty("commissionPriceCategoryId").GetString();
            var g = unmatched.SingleOrDefault(x => x.ProgramId == p.GetProperty("programId").GetString() && x.SupplyChainHojreId == line.Quote.SupplierKey
                && x.EventId == (line.Quote.OfferKey.StartsWith("service:") ? null : line.Quote.OfferKey)
                && x.ProgramTicketTypeId == p.GetProperty("ProgramTicketTypeId").GetString()
                && x.EventTicketTypeId == p.GetProperty("EventTicketTypeId").GetString()
                && x.CommissionPriceCategoryId == category);
            var retail = p.TryGetProperty("pricingBasis", out var basis) && basis.GetString() == "ProviderRetail";
            // Only the initial reservation may pin a lower cost; customer retail and later fulfillment stay fixed.
            decimal? cost = captureRetailCost && retail ? g?.Tickets?.FirstOrDefault()?.BuyPrice : line.Quote.ProviderCostMinor;
            if (g?.Tickets == null || g.Tickets.Count != line.Quote.Quantity || g.TicketsCount != line.Quote.Quantity
                || !line.Quote.ProviderCostMinor.HasValue || !cost.HasValue || cost <= 0
                || cost > line.Quote.ProviderCostMinor || decimal.Truncate(cost.Value) != cost.Value
                || g.Tickets.Any(x => x.BuyPrice != cost.Value)
                || g.TotalBuyPrice != cost.Value * line.Quote.Quantity
                || g.TotalTicketPrice != g.Tickets.Sum(x => x.TicketPrice)
                || retail && (g.Tickets.Any(x => x.TicketPrice != line.Quote.UnitPriceMinor)
                        || g.TotalTicketPrice != checked(line.Quote.UnitPriceMinor * line.Quote.Quantity))
                || g.Tickets.Select(x => x.TicketId).Distinct().Count() != line.Quote.Quantity)
            {
                logger?.LogWarning("TouristPanel reservation line mismatch Cart={Cart} MatchedGroup={MatchedGroup} ExpectedQuantity={ExpectedQuantity} TicketsCount={TicketsCount} QuotedCost={QuotedCost} ReservedCost={ReservedCost} GroupBuyTotal={GroupBuyTotal} Retail={Retail}",
                    cart.Id, g != null, line.Quote.Quantity, g?.TicketsCount, line.Quote.ProviderCostMinor, g?.Tickets?.FirstOrDefault()?.BuyPrice, g?.TotalBuyPrice, line.Quote.UnitPriceMinor);
                throw new EntertainmentProviderAmbiguousException("تعداد یا بهای رزرو با انتخاب متفاوت است");
            }
            if (line.Quote.RequiresManifest || line.Quote.RequiresIdentityNumber)
            {
                var actual = g.Tickets.Select(x => (x.ManifestUniqueName ?? "").Trim() + "|" + (line.Quote.RequiresIdentityNumber ? x.ManifestUniqueNumber?.Trim() : "")).Order().ToArray();
                var expected = line.Passengers.Select(x => x.Name.Trim() + "|" + (line.Quote.RequiresIdentityNumber ? x.IdentityNumber?.Trim() : "")).Order().ToArray();
                if (!actual.SequenceEqual(expected)) throw new EntertainmentProviderAmbiguousException("مشخصات بلیط‌های رزرو تطبیق ندارد");
            }
            var quote = line.Quote;
            if (cost.Value != quote.ProviderCostMinor)
            {
                var reservedCost = checked((long)cost.Value);
                var adjustment = checked(quote.UnitPriceMinor - reservedCost);
                var payloadSnapshot = JsonNode.Parse(quote.ProviderPayloadJson)!.AsObject();
                payloadSnapshot["quotedProviderCostMinor"] = quote.ProviderCostMinor;
                payloadSnapshot["providerCostMinor"] = reservedCost;
                payloadSnapshot["providerRetailAdjustmentMinor"] = adjustment;
                logger?.LogInformation("TouristPanel reserved cost decreased Cart={Cart} QuotedCost={QuotedCost} ReservedCost={ReservedCost} Retail={Retail}",
                    cart.Id, quote.ProviderCostMinor, reservedCost, quote.UnitPriceMinor);
                quote = quote with { ProviderCostMinor = reservedCost, ProviderRetailAdjustmentMinor = adjustment,
                    ProviderPayloadJson = payloadSnapshot.ToJsonString(TouristPanelClient.Json) };
            }
            reservedLines.Add(line with { Quote = quote });
            unmatched.Remove(g);
        }
        var totalTicketPrice = cart.Groups.Sum(x => x.TotalTicketPrice);
        // TouristPanel can leave the temporary cart total at zero; ticket/group prices are validated above.
        if (cart.TotalAmount != 0 && totalTicketPrice != cart.TotalAmount || cart.DiscountAmount != 0)
        {
            logger?.LogWarning("TouristPanel cart totals mismatch Cart={Cart} TotalAmount={TotalAmount} GroupTicketTotal={GroupTicketTotal} DiscountAmount={DiscountAmount}",
                cart.Id, cart.TotalAmount, totalTicketPrice, cart.DiscountAmount);
            throw new EntertainmentProviderAmbiguousException("جمع مالی سبد رزرو قابل تطبیق نیست");
        }
        return context with { Lines = reservedLines };
    }

    private EntertainmentFulfillmentResult MapPurchase(TpPurchaseResultDto result, ReservationContext context, string operationId)
    {
        var invoice = result.TicketsInvoice;
        var id = invoice?.Id;
        EntertainmentFulfillmentResult Pending(string status) => new(id ?? "", [])
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
        if (invoice.TotalSupplierBuyAmount != context.Lines.Sum(x => checked(x.Quote.ProviderCostMinor * x.Quote.Quantity))
            || tickets.Any(x => x.IsCanceled || x.IsRevoked)) return Pending("ManualReview");
        foreach (var line in context.Lines)
        {
            using var payload = JsonDocument.Parse(line.Quote.ProviderPayloadJson);
            var p = payload.RootElement;
            var type = payload.RootElement.GetProperty("ProgramTicketTypeId").GetString();
            var eventType = payload.RootElement.GetProperty("EventTicketTypeId").GetString();
            var category = payload.RootElement.GetProperty("CommissionPriceCategory").GetProperty("commissionPriceCategoryId").GetString();
            var matching = groups.Where(g => g.ProgramId == p.GetProperty("programId").GetString() && g.SupplyChainHojreId == line.Quote.SupplierKey
                && g.ProgramTicketTypeId == type && g.EventTicketTypeId == eventType
                && g.EventId == (line.Quote.OfferKey.StartsWith("service:") ? null : line.Quote.OfferKey))
                .SelectMany(g => g.Tickets ?? []).Where(t => t.CommissionPriceCategoryId == category).ToArray();
            if (matching.Length != line.Quote.Quantity || matching.Any(t => t.SupplierBuyAmount != line.Quote.ProviderCostMinor)) return Pending("ManualReview");
            if (line.Quote.RequiresManifest || line.Quote.RequiresIdentityNumber)
            {
                var actual = matching.Select(t => (t.ManifestUniqueName ?? "").Trim() + "|" + (line.Quote.RequiresIdentityNumber ? t.ManifestUniqueNumber?.Trim() : "")).Order();
                var expected = line.Passengers.Select(p => p.Name.Trim() + "|" + (line.Quote.RequiresIdentityNumber ? p.IdentityNumber?.Trim() : "")).Order();
                if (!actual.SequenceEqual(expected)) return Pending("ManualReview");
            }
        }
        var documents = new List<EntertainmentDeliveryDocument>();
        void Add(string title, string? url)
        {
            if (Uri.TryCreate(url, UriKind.Absolute, out var uri) && uri.Scheme == "https"
                && O.DeliveryHosts.Contains(uri.Host, StringComparer.OrdinalIgnoreCase)) documents.Add(new(title, uri.ToString()));
        }
        Add("دریافت بلیط‌ها", result.TicketsUrl); Add("دریافت فاکتور", result.InvoiceUrl); Add("نمایش بلیط‌ها", result.PublicTicketsUrl);
        foreach (var group in result.TicketTypeGroupUrls ?? []) Add(group.ProgramTitle + " - " + group.TicketType, group.Url);
        return new(id!, groups.SelectMany(g => (g.Tickets ?? []).Select(t => new EntertainmentIssuedTicket(t.TicketNo ?? t.Id!, false)
        { ProviderTicketId = t.Id, AdmissionCode = t.TicketNo, ProgramTitle = g.ProgramTitle, VenueTitle = g.SupplyChainHojreTitle, Address = g.ProgramAddress, StartsAt = ParseDate(g.EventStartDateTime), Title = g.ProgramTicketTypeTitle ?? g.TicketTypeTitle, QrCode = t.QrCode, GroupId = g.GroupId })).ToArray())
        { ProviderInvoiceId = id, ProviderPaymentId = result.PaymentId, Documents = documents };
    }
    private Task<EntertainmentProviderReceipt?> Receipt(string operation, CancellationToken ct) => db.ProviderReceipts.SingleOrDefaultAsync(x => x.OperationId == operation && x.AccountKey == O.AccountKey, ct);
    private async Task SaveReceipt<T>(string operation, T payload, CancellationToken ct)
    {
        var receipt = await Receipt(operation, ct);
        var json = secrets.Protect(JsonSerializer.Serialize(payload, TouristPanelClient.Json));
        if (receipt is null) db.ProviderReceipts.Add(EntertainmentProviderReceipt.Create(operation, O.AccountKey, json));
        else receipt.Update(json);
        await db.SaveChangesAsync(ct);
    }
    private sealed record ReservationCheckpoint(IReadOnlyList<TpAddTempGroupDto> Groups, TpTempShoppingCartDto? Cart);
    private sealed record PurchaseCheckpoint(ReservationContext Context, TpPurchaseResultDto? Result)
    { public bool Rejected { get; init; } }
}
