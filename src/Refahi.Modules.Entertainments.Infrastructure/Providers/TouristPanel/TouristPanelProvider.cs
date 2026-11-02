using Microsoft.Extensions.Logging;
using Refahi.Modules.Entertainments.Application.Contracts.Discovery;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Refahi.Modules.Entertainments.Application.Contracts;
using Refahi.Modules.Entertainments.Application.Contracts.Providers;
using Refahi.Modules.Entertainments.Application.Contracts.Providers.Dtos;
using Refahi.Modules.Entertainments.Application.Contracts.Providers.Requests;
using Refahi.Modules.Entertainments.Domain;
using Refahi.Modules.Entertainments.Infrastructure.Persistence;
using Refahi.Modules.Entertainments.Infrastructure.Providers.TouristPanel.Contracts;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Refahi.Modules.Entertainments.Infrastructure.Providers.TouristPanel;

public sealed partial class TouristPanelProvider(TouristPanelClient client,
    TouristPanelReadCache readCache, IOptions<TouristPanelOptions> options, IEntertainmentPricingService pricing, EntertainmentDbContext db, IEntertainmentSecretProtector secrets, IEntertainmentProgramIdentity discovery, ILogger<TouristPanelProvider>? logger = null)
    : IEntertainmentProvider, IEntertainmentOfferProvider, IEntertainmentReservationProvider, IEntertainmentFulfillmentStatusProvider, IEntertainmentReservationRecoveryProvider
{
    public string Key => "touristpanel";
    public string Name => "توریست‌پنل";
    public EntertainmentProviderCapabilities Capabilities => new(false, false);
    private TouristPanelOptions O => options.Value;
    public async Task<EntertainmentPurchaseRequirements> GetRequirementsAsync(string programKey, CancellationToken ct)
    {
        var native = await discovery.ResolveNativeAsync(programKey, ct) ?? throw Error("خدمت در دسترس نیست");
        var program = await client.GetDetailAsync(native, null, ct); ValidateProgram(program, native);
        if (!program.IsActive || (int)program.Type is not (1 or 2)) throw Error("خدمت قابل خرید نیست");
        return new(program.IsManifestNeeded, program.IsManifestUniqueNumberNeeded);
    }
    public async Task<IReadOnlyList<EntertainmentOfferDto>> GetOffersAsync(string programKey, DateOnly? start, DateOnly? end, CancellationToken ct)
    {
        var native = await discovery.ResolveNativeAsync(programKey, ct) ?? throw Error("خدمت در دسترس نیست");
        var p = await readCache.GetDetailAsync(native, ct);

        ValidateProgram(p, native);

        return await Offers(p, start, end, ct);
    }

    private async Task<IReadOnlyList<EntertainmentOfferDto>> Offers(TpProgramDto p, DateOnly? start, DateOnly? end, CancellationToken ct)
    {
        if (!p.IsActive || (int)p.Type is not (1 or 2))
            return [];

        if ((int)p.Type == 1)
            return [
                MakeOffer(
                    p, "service:" + p.Id,
                    "انتخاب بلیط",
                    null,
                    null,
                    await readCache.GetProgramTicketsAsync(p.Id!, p.SupplyChainHojreId!, ct)
                )
            ];

        if ((start.HasValue != end.HasValue) || (start.HasValue && (end < start || end.GetValueOrDefault().DayNumber - start.Value.DayNumber > 6)))
            throw Error("بازه انتخاب سانس باید حداکثر هفت روز باشد");

        var days = await readCache.GetEventsAsync(
            p.Id!,
            p.SupplyChainHojreId!,
            start,
            end,
            null,
            true,
            ct
        );

        var result = new List<EntertainmentOfferDto>();

        foreach (var e in days.SelectMany(x => x.Events ?? []))
        {
            if (!Guid.TryParse(e.EventId, out _))
                throw Error("شناسه سانس معتبر نیست");

            // A 30-day calendar is lightweight. Ticket details are loaded only for an explicit range.
            // Embedded event prices can differ from the account-specific ticket endpoint used by checkout.
            var types = start.HasValue
                ? await readCache.GetEventTicketsAsync(e.EventId!, p.SupplyChainHojreId!, null, ct)
                : e.TicketTypes ?? [];

            result.Add(
                MakeOffer(
                    p,
                    e.EventId!,
                    e.Label ?? e.Start ?? "سانس",
                    ParseDate(e.ExcuteTime),
                    e.Capacity,
                    types
                )
            );
        }

        return result;
    }
    private EntertainmentOfferDto MakeOffer(TpProgramDto p, string key, string title, DateTimeOffset? starts, int? capacity, IReadOnlyList<TpTicketTypeDto> types)
    {
        var options = types.Select(t => NormalizeTicketType(t)).Where(t => ValidTicketType(p, t)).Select(t => PurchaseOption(p, key, t)).ToArray();
        foreach (var group in options.Where(x => !x.IsAvailable).GroupBy(x => x.UnavailableReasonCode))
            logger?.LogInformation("TouristPanel unavailable options Program={Program} Offer={Offer} Reason={Reason} Count={Count}", p.Id, key, group.Key, group.Count());
        var invalidTypes = types.Count - options.Length;
        if (invalidTypes > 0) logger?.LogWarning("TouristPanel invalid ticket identities Program={Program} Count={Count}", p.Id, invalidTypes);
        return new(
            Key,
            p.SupplyChainHojreId!,
            p.Id!,
            key,
            title,
            null,
            starts,
            capacity,
            Category(p),
            options
        );

    }

    private EntertainmentPurchaseOptionDto PurchaseOption(TpProgramDto program, string offerKey, TpTicketTypeDto type)
    {
        var selected = CheapestPrice(type);
        var sale = selected is null ? null : Sale(selected);
        var reason = sale is null ? "PriceUnavailable"
            : type.IsDisabled ? "TicketDisabled"
            : (int)program.Type == 2 && type.RemainingCapacity <= 0 ? "SoldOut"
            : !CanSell(program, type) ? "SalesUnavailable" : null;
        return new(StableOptionKey(program.Id!, offerKey, type), type.Title ?? "بلیط", sale?.SaleMinor ?? 0, null, reason is null)
        { UnavailableReasonCode = reason };
    }

    private TpTicketPriceDto? CheapestPrice(TpTicketTypeDto type)
    {
        var identified = (type.TicketPrices ?? []).Where(x =>
            Guid.TryParse(x.CommissionPriceCategory?.CommissionPriceCategoryId, out var id) && id != Guid.Empty);
        var unique = new List<TpTicketPriceDto>();
        foreach (var group in identified.GroupBy(x => Guid.Parse(x.CommissionPriceCategory!.CommissionPriceCategoryId!)))
        {
            // Different costs or terms for one provider identity cannot be selected safely.
            var first = group.First();
            if (group.Any(x => !SamePrice(first, x)))
            {
                logger?.LogWarning("TouristPanel conflicting prices Type={Type} Category={Category}", type.ProgramTicketTypeId, group.Key);
                return null;
            }
            unique.Add(first);
        }
        return unique.Where(x => x.CommissionPriceCategory!.Currency == TpCurrencyType.IRR
                && x.BuyPrice > 0 && x.BuyPrice <= long.MaxValue && decimal.Truncate(x.BuyPrice) == x.BuyPrice
                && (!O.UseProviderSalePrice || x.TicketPrice > 0 && x.TicketPrice <= long.MaxValue && decimal.Truncate(x.TicketPrice) == x.TicketPrice))
            .OrderBy(x => x.BuyPrice)
            .ThenBy(x => Guid.Parse(x.CommissionPriceCategory!.CommissionPriceCategoryId!).ToString("D"), StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault();
    }

    private static bool SamePrice(TpTicketPriceDto first, TpTicketPriceDto second)
    {
        TpTicketPriceDto Normalize(TpTicketPriceDto price) => price with
        {
            CommissionPriceCategory = price.CommissionPriceCategory! with
            { CommissionPriceCategoryId = Guid.Parse(price.CommissionPriceCategory.CommissionPriceCategoryId!).ToString("D") },
            CommissionPriceGroupId = Guid.TryParse(price.CommissionPriceGroupId, out var group) ? group.ToString("D") : price.CommissionPriceGroupId,
            Beneficiaries = null
        };
        return Normalize(first) == Normalize(second)
            && (first.Beneficiaries ?? []).SequenceEqual(second.Beneficiaries ?? []);
    }

    private static TpTicketTypeDto NormalizeTicketType(TpTicketTypeDto type) => type with
    {
        // The live API represents "no reference ticket" with Guid.Empty when no dependency is required.
        ReferenceTicketTypeId = !type.IsNumberEqualsReferenceTicketType && Guid.TryParse(type.ReferenceTicketTypeId, out var reference) && reference == Guid.Empty
            ? null : type.ReferenceTicketTypeId
    };

    private static bool ValidTicketType(TpProgramDto p, TpTicketTypeDto t)
    {
        return Guid.TryParse(t.ProgramTicketTypeId, out var programType) &&
            programType != Guid.Empty &&
            ((int)p.Type == 1 || Guid.TryParse(t.EventTicketTypeId, out var eventType) &&
            eventType != Guid.Empty) &&
            (
                string.IsNullOrWhiteSpace(t.ReferenceTicketTypeId) && !t.IsNumberEqualsReferenceTicketType ||
                Guid.TryParse(t.ReferenceTicketTypeId, out var reference) &&
                reference != Guid.Empty
            );
    }

    private bool CanSell(TpProgramDto p, TpTicketTypeDto t)
    {
        return O.
            IsSaleConfigured &&
            p.IsActive && !t.IsDisabled &&
            (!(p.IsManifestNeeded || p.IsManifestUniqueNumberNeeded) || O.ManifestConfirmed) &&
            (!(t.IsNumberEqualsReferenceTicketType || !string.IsNullOrWhiteSpace(t.ReferenceTicketTypeId)) || O.ReferenceTicketsConfirmed) &&
            ((int)p.Type == 1 || (O.DateRangeConfirmed && !string.IsNullOrWhiteSpace(O.TimeZoneId) && t.RemainingCapacity > 0)
        );
    }

    private EntertainmentPrice? Sale(TpTicketPriceDto selected)
    {
        if (!O.MarkupPercent.HasValue || !O.MarkupFixedMinor.HasValue || string.IsNullOrWhiteSpace(O.PricingVersion)) return null;
        try
        {
            if (O.UseProviderSalePrice)
            {
                if (selected.TicketPrice <= 0 || selected.TicketPrice > long.MaxValue || decimal.Truncate(selected.TicketPrice) != selected.TicketPrice) return null;
                var cost = checked((long)selected.BuyPrice);
                var retail = checked((long)selected.TicketPrice);
                return new(cost, 0, retail, O.PricingVersion) { ProviderRetailAdjustmentMinor = checked(retail - cost) };
            }
            return pricing.Calculate(selected.BuyPrice, O.MarkupPercent.Value, O.MarkupFixedMinor.Value, O.PricingVersion,
                O.DiscountPercent, O.DiscountFixedMinor);
        }
        catch (Exception ex) when (ex is EntertainmentDomainException or OverflowException)
        {
            logger?.LogWarning("TouristPanel invalid retail price PricingVersion={PricingVersion} ErrorType={ErrorType}", O.PricingVersion, ex.GetType().Name);
            return null;
        }
    }

    private string Category(TpProgramDto p) => p.ProgramCategory != null
        && O.CategoryCodes.TryGetValue(p.ProgramCategory.Code, out var code) && !string.IsNullOrWhiteSpace(code)
            ? code : "entertainment";

    public static string StableOptionKey(string program, string offer, TpTicketTypeDto type)
    {
        var eventId = offer.StartsWith("service:", StringComparison.Ordinal) ? "" : Guid.Parse(offer).ToString("N");
        var identity = $"v2|{Guid.Parse(program):N}|{eventId}|{Guid.Parse(type.ProgramTicketTypeId!):N}|{(Guid.TryParse(type.EventTicketTypeId, out var id) ? id.ToString("N") : "")}";
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(identity))).ToLowerInvariant();
    }

    // Retained only for recognizing cart selections issued before stable v2 identities.
    public static string OptionKey(TpTicketTypeDto t, TpTicketPriceDto price)
    {
        return Convert.ToHexString(
                    SHA256.HashData(
                        Encoding.UTF8.GetBytes(
                                $"{t.ProgramTicketTypeId?.ToLowerInvariant()}|{t.EventTicketTypeId?.ToLowerInvariant()}|{price.CommissionPriceCategory?.CommissionPriceCategoryId?.ToLowerInvariant()}"
                        )
                    )
                ).ToLowerInvariant();
    }

    private string? Image(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        if (Uri.TryCreate(value, UriKind.Absolute, out var absolute))
            return absolute.Scheme == "https"
                ? absolute.ToString()
                : null;

        return Uri.TryCreate(new Uri(O.ImageBaseUrl), value, out var uri) &&
            uri.Scheme == "https" &&
            uri.Host == new Uri(O.ImageBaseUrl).Host
                ? uri.ToString()
                : null;
    }

    private DateTimeOffset? ParseDate(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        if (value.EndsWith('Z') || System.Text.RegularExpressions.Regex.IsMatch(value, @"[+-]\d\d:\d\d$"))
            return DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out var offset) ? offset : null;

        if (string.IsNullOrWhiteSpace(O.TimeZoneId) || !DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
            return null;

        var zone = TimeZoneInfo.FindSystemTimeZoneById(O.TimeZoneId);

        date = DateTime.SpecifyKind(date, DateTimeKind.Unspecified);

        if (zone.IsAmbiguousTime(date) || zone.IsInvalidTime(date))
            return null;

        return new DateTimeOffset(date, zone.GetUtcOffset(date));
    }
    private static void ValidateProgram(TpProgramDto p, string requested)
    {
        if (!Guid.TryParse(p.Id, out var id) || !Guid.TryParse(requested, out var expected) || id != expected || !Guid.TryParse(p.SupplyChainHojreId, out var supplier) || supplier == Guid.Empty)
            throw Error("پاسخ برنامه با درخواست سازگار نیست");
    }

    private static EntertainmentDomainException Error(string message) =>
        new(message, "TOURIST_PANEL_VALIDATION");

    private sealed record Selection(TpProgramDto Program, TpTicketTypeDto Type, TpTicketPriceDto Price, EntertainmentQuoteResult Quote);
    private sealed record ReservationContext(string AccountKey, IReadOnlyList<EntertainmentReservedLine> Lines, TpTempShoppingCartDto Cart);

    public async Task<EntertainmentQuoteResult> QuoteAsync(EntertainmentQuoteRequest request, CancellationToken ct) =>
        (await Select(request, ct)).Quote;

    private async Task<Selection> Select(EntertainmentQuoteRequest request, CancellationToken ct)
    {
        if (request.Quantity is < 1 or > 100)
            throw Error("تعداد بلیط معتبر نیست");

        var native = await discovery.ResolveNativeAsync(request.ProgramKey, ct) ?? throw Error("خدمت در دسترس نیست");
        var p = await client.GetDetailAsync(native, null, ct);

        ValidateProgram(p, native);

        List<TpTicketTypeDto> types;

        if ((int)p.Type == 1 && request.OfferKey == "service:" + p.Id)
        {
            types = await client.GetProgramTicketsAsync(p.Id!, p.SupplyChainHojreId!, ct);
        }
        else if ((int)p.Type == 2)
        {
            if (!Guid.TryParse(request.OfferKey, out var eventId) || eventId == Guid.Empty)
                throw Error("شناسه سانس معتبر نیست");

            // The event endpoint defaults to only the next 30 days when no range is supplied. The selected Event id
            // is therefore resolved through its dedicated endpoint; the external cart revalidates Program + Event.
            types = await client.GetEventTicketsAsync(request.OfferKey, p.SupplyChainHojreId!, null, ct);

            if (types.Count == 0 || types.Any(x => !Guid.TryParse(x.EventTicketTypeId, out var id) || id == Guid.Empty))
                throw Error("سانس یا نوع بلیط در دسترس نیست");
        }
        else
        {
            throw Error("نوع برنامه یا گزینه خرید پشتیبانی نمی‌شود");
        }

        var matches = types.Select(t => NormalizeTicketType(t)).Where(t => ValidTicketType(p, t))
            .Where(t => StableOptionKey(p.Id!, request.OfferKey, t) == request.PurchaseOptionKey
                || (t.TicketPrices ?? []).Any(price => OptionKey(t, price) == request.PurchaseOptionKey))
            .ToArray();
        if (matches.Length != 1)
            throw Error("گزینه بلیط تغییر کرده است؛ آن را از سبد حذف و دوباره انتخاب کنید");
        var t = matches[0];
        var selected = CheapestPrice(t) ?? throw Error("قیمت معتبر برای این بلیط در دسترس نیست");

        if (!CanSell(p, t) || ((int)p.Type == 2 && t.RemainingCapacity < request.Quantity))
            throw Error("خرید یا ظرفیت این بلیط در دسترس نیست");

        var priceResult = Sale(selected)
            ?? throw Error("قاعده قیمت تنظیم نشده است");
        try
        {
            _ = checked(priceResult.ProviderCostMinor * request.Quantity);
            _ = checked(priceResult.SaleMinor * request.Quantity);
        }
        catch (OverflowException) { throw Error("مبلغ کل بلیط‌ها معتبر نیست"); }


        var payload = JsonSerializer.Serialize(new
        {
            programId = p.Id,
            supplyChainHojreId = p.SupplyChainHojreId,
            eventId = (int)p.Type == 2
                ? request.OfferKey
                : null,
            t.ProgramTicketTypeId,
            t.EventTicketTypeId,
            t.ReferenceTicketTypeId,
            t.IsNumberEqualsReferenceTicketType,
            selected.CommissionPriceCategory,
            providerTicketPrice = selected.TicketPrice,
            providerCommissionPrice = selected.CommissionPrice,
            pricingBasis = O.UseProviderSalePrice ? "ProviderRetail" : "SupplierCost",
            providerRetailAdjustmentMinor = priceResult.ProviderRetailAdjustmentMinor,
            markupPercent = O.MarkupPercent,
            markupFixedMinor = O.MarkupFixedMinor,
            discountPercent = O.DiscountPercent,
            discountFixedMinor = O.DiscountFixedMinor,
            discountMinor = priceResult.DiscountMinor,
            providerCostMinor = priceResult.ProviderCostMinor,
            saleMinor = priceResult.SaleMinor,
            markupMinor = priceResult.MarkupMinor,
            pricingVersion = priceResult.Version
        });

        var quote = new EntertainmentQuoteResult(
            Key,
            p.SupplyChainHojreId!,
            Guid.Parse(request.ProgramKey).ToString("N"),
            request.OfferKey,
            StableOptionKey(p.Id!, request.OfferKey, t),
            p.SupplyChainHojre?.Title ?? "فروشنده",
            p.Title ?? "خدمت", (await db.Programs.AsNoTracking().Where(x => x.ProgramKey == Guid.Parse(request.ProgramKey)).Select(x => x.Images).SingleOrDefaultAsync(ct))?.FirstOrDefault(),
            (int)p.Type == 1
                ? "بدون سانس"
                : t.EventTitle ?? "سانس انتخاب‌شده",
            t.Title ?? "بلیط",
            Category(p)!,
            request.Quantity,
            priceResult.SaleMinor,
            priceResult.SaleMinor,
            (int)p.Type == 1 && t.RemainingCapacity == 0
                ? null
                : t.RemainingCapacity,
            payload
        )
        {
            ProviderCostMinor = priceResult.ProviderCostMinor,
            MarkupMinor = priceResult.MarkupMinor,
            DiscountMinor = priceResult.DiscountMinor,
            ProviderRetailAdjustmentMinor = priceResult.ProviderRetailAdjustmentMinor,
            PreviousPurchaseOptionKeys = (t.TicketPrices ?? []).Select(price => OptionKey(t, price)).Distinct().ToArray(),
            PricingVersion = priceResult.Version,
            RequiresManifest = p.IsManifestNeeded,
            RequiresIdentityNumber = p.IsManifestUniqueNumberNeeded
        };

        return new(p, t, selected, quote);
    }

    public Task CancelAsync(EntertainmentCancellationRequest request, CancellationToken ct) =>
        throw Error("لغو خودکار این بلیت صادرشده پشتیبانی نمی‌شود");
}
