using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Refahi.Modules.Commerce.Application.Contracts;
using Refahi.Modules.Commerce.Application.Contracts.Providers;
using Refahi.Modules.Commerce.Application.Contracts.Providers.Dtos;
using Refahi.Modules.Commerce.Application.Contracts.Providers.Requests;
using Refahi.Modules.Commerce.Domain;
using Refahi.Modules.Commerce.Infrastructure.Persistence;
using Refahi.Modules.Commerce.Infrastructure.Providers.TouristPanel.Contracts;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Refahi.Modules.Commerce.Infrastructure.Providers.TouristPanel;

public sealed partial class TouristPanelCommerceProvider(TouristPanelClient client, TouristPanelCatalog catalog,
    TouristPanelReadCache readCache, IOptions<TouristPanelOptions> options, ICommercePricingService pricing, CommerceDbContext db, ICommerceSecretProtector secrets)
    : ICommerceProvider, ICommerceOfferProvider, ICommerceReservationProvider, ICommerceFulfillmentStatusProvider
{
    public string Key => "touristpanel";
    public string Name => "توریست‌پنل";
    public CommerceProviderCapabilities Capabilities => new(false, false);
    private TouristPanelOptions O => options.Value;
    private static readonly CommerceAddressDto EmptyAddress = new("", "", "", null);
    public async Task<IReadOnlyList<CommerceSellerDto>> GetSellersAsync(CancellationToken ct)
    {
        var query = await catalog.ReadAsync(ct);

        return query.GroupBy(x => x.SupplyChainHojreId!, StringComparer.OrdinalIgnoreCase)
                    .Select(g => new CommerceSellerDto(
                        Key,
                        g.Key,
                        g.First().SupplyChainHojre?.Title ?? "فروشنده",
                        g.Select(x => x.ProgramCategory?.Title).OfType<string>().Distinct().ToArray(),
                        Image(g.First().SupplyChainHojre?.Logo),
                        null,
                        null,
                        EmptyAddress
                    ))
                    .ToArray();
    }

    public async Task<CommerceSellerDto?> GetSellerAsync(string sellerKey, CancellationToken ct)
    {
        var query = await GetSellersAsync(ct);

        return query.SingleOrDefault(x => x.SellerKey.Equals(sellerKey, StringComparison.OrdinalIgnoreCase));
    }

    public async Task<CommerceCatalogPage> GetProductsAsync(CommerceCatalogQuery query, CancellationToken ct)
    {
        var list = await catalog.ReadAsync(ct);

        var rows = list.Where(x => x.IsActive && (int)x.Type is 1 or 2)
                       .Where(x => string.IsNullOrWhiteSpace(query.SellerKey) || string.Equals(query.SellerKey, x.SupplyChainHojreId, StringComparison.OrdinalIgnoreCase))
                       .Where(x => string.IsNullOrWhiteSpace(query.Search) || (x.Title ?? "").Contains(query.Search, StringComparison.OrdinalIgnoreCase))
                        .OrderBy(x => x.Title).ThenBy(x => x.Id).ToArray();

        return new(
            rows.Skip((query.PageNumber - 1) * query.PageSize)
                .Take(query.PageSize)
                .Select(x => new CommerceProductDto(
                    Key,
                    x.SupplyChainHojreId!,
                    x.Id!, x.Title ?? "خدمت",
                    null,
                    Image(x.IndexImage),
                    x.Tag?.Title is { } tag ? [tag] : [],
                    new(
                        x.LocationData?.State?.Native ?? "",
                        x.LocationData?.City?.Native ?? "", "", null
                    ),
                    []
                )
                {
                    ProgramType = (int)x.Type,
                    LocationCode = x.LocationData?.Code,
                    ExternalCategoryCode = x.ProgramCategory?.Code.ToString()
                }).ToArray(),
                query.PageNumber,
                query.PageSize,
                rows.Length
            );
    }
    public async Task<CommerceProductDto?> GetProductAsync(string productKey, CancellationToken ct)
    {
        TpProgramDto p;

        try
        {
            p = await readCache.GetDetailAsync(productKey, ct);
        }
        catch (TouristPanelHttpException ex) when (ex.StatusCode == 404)
        {
            return null;
        }

        ValidateProgram(p, productKey);

        var offers = (int)p.Type == 1
            ? await Offers(p, null, null, ct)
            : [];

        return new(
            Key,
            p.SupplyChainHojreId!,
            p.Id!,
            p.Title ?? "خدمت",
            p.Description,
            Image(p.IndexImage),
            p.Tag?.Title is { } tag
                ? [tag]
                : [],
            new("", p.Location?.Native ?? "", p.ProgramAddress ?? "", null),
            offers
        )
        {
            ProgramType = (int)p.Type,
            IsActive = p.IsActive,
            Rules = p.Rules,
            Gallery = (p.GalleryImages ?? [])
                .Select(Image)
                .OfType<string>()
                .ToArray(),
            RequiresManifest = p.IsManifestNeeded,
            RequiresIdentityNumber = p.IsManifestUniqueNumberNeeded,
            LocationCode = p.Location?.Code,
            ExternalCategoryCode = p.ProgramCategory?.Code.ToString(),
            UnavailableReason = !O.IsSaleConfigured
                ? "فروش این خدمت هنوز فعال نشده است"
                : !p.IsActive || (int)p.Type is not (1 or 2)
                    ? "خدمت قابل خرید نیست"
                    : null
        };
    }
    public async Task<CommerceCatalogFilters> GetFiltersAsync(CancellationToken ct)
    {
        var rows = await catalog.ReadAsync(ct);

        return new(
            rows.Where(x => x.LocationData?.Code != null)
                .DistinctBy(x => x.LocationData!.Code)
                .Select(x =>
                    new CommerceFilterOption(
                        x.LocationData!.Code!,
                        x.LocationData.Native ?? ""
                    )
                ).ToArray(),

            rows.Where(x => x.ProgramCategory != null)
                .DistinctBy(x => x.ProgramCategory!.Code)
                .Select(x =>
                    new CommerceFilterOption(
                        x.ProgramCategory!.Code.ToString(),
                        x.ProgramCategory.Title ?? ""
                    )
                ).ToArray()
        );
    }

    public async Task<IReadOnlyList<CommerceOfferDto>> GetOffersAsync(string productKey, DateOnly? start, DateOnly? end, CancellationToken ct)
    {
        var p = await readCache.GetDetailAsync(productKey, ct);

        ValidateProgram(p, productKey);

        return await Offers(p, start, end, ct);
    }

    private async Task<IReadOnlyList<CommerceOfferDto>> Offers(TpProgramDto p, DateOnly? start, DateOnly? end, CancellationToken ct)
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

        var result = new List<CommerceOfferDto>();

        foreach (var e in days.SelectMany(x => x.Events ?? []))
        {
            if (!Guid.TryParse(e.EventId, out _))
                throw Error("شناسه سانس معتبر نیست");

            // A 30-day calendar is lightweight. Ticket details are loaded only for an explicit range.
            var types = e.TicketTypes ?? (start.HasValue ? await readCache.GetEventTicketsAsync(e.EventId!, p.SupplyChainHojreId!, null, ct) : []);

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
    private CommerceOfferDto MakeOffer(TpProgramDto p, string key, string title, DateTimeOffset? starts, int? capacity, IReadOnlyList<TpTicketTypeDto> types)
    {
        return new(
            Key,
            p.SupplyChainHojreId!,
            p.Id!,
            key,
            title,
            null,
            starts,
            capacity,
            Category(p) ?? "",
            types.Where(t => ValidTicketType(p, t))
                 .SelectMany(t => Options(p, t))
                 .ToArray()
        );

    }

    private IEnumerable<CommercePurchaseOptionDto> Options(TpProgramDto program, TpTicketTypeDto type)
    {
        var prices = EligiblePrices(type).ToArray();

        return prices.Select(price =>
            new CommercePurchaseOptionDto(
                OptionKey(type, price),
                prices.Length == 1
                    ? type.Title ?? "بلیط"
                    : $"{type.Title ?? "بلیط"} - {price.CommissionPriceCategory?.Title}",
                Sale(price.BuyPrice)?.SaleMinor ?? 0,
                null,
                CanSell(program, type) && Sale(price.BuyPrice) is { SaleMinor: > 0 }
            )
        );
    }

    private IEnumerable<TpTicketPriceDto> EligiblePrices(TpTicketTypeDto t)
    {
        return (t.TicketPrices ?? [])
            .Where(x =>
                x.CommissionPriceCategory?.Currency == TpCurrencyType.IRR &&
                Guid.TryParse(x.CommissionPriceCategory.CommissionPriceCategoryId, out var categoryId) &&
                categoryId != Guid.Empty &&
                O.AllowedPriceCategoryIds.Contains(x.CommissionPriceCategory.CommissionPriceCategoryId, StringComparer.OrdinalIgnoreCase)
            );
    }

    private static bool ValidTicketType(TpProgramDto p, TpTicketTypeDto t)
    {
        return Guid.TryParse(t.ProgramTicketTypeId, out var programType) &&
            programType != Guid.Empty &&
            ((int)p.Type == 1 || Guid.TryParse(t.EventTicketTypeId, out var eventType) &&
            eventType != Guid.Empty) &&
            (
                string.IsNullOrWhiteSpace(t.ReferenceTicketTypeId) ||
                Guid.TryParse(t.ReferenceTicketTypeId, out var reference) &&
                reference != Guid.Empty
            );
    }

    private bool CanSell(TpProgramDto p, TpTicketTypeDto t)
    {
        return O.
            IsSaleConfigured &&
            p.IsActive && !t.IsDisabled &&
            Category(p) != null &&
            (!(p.IsManifestNeeded || p.IsManifestUniqueNumberNeeded) || O.ManifestConfirmed) &&
            (!(t.IsNumberEqualsReferenceTicketType || !string.IsNullOrWhiteSpace(t.ReferenceTicketTypeId)) || O.ReferenceTicketsConfirmed) &&
            ((int)p.Type == 1 || (O.DateRangeConfirmed && !string.IsNullOrWhiteSpace(O.TimeZoneId) && t.RemainingCapacity > 0)
        );
    }

    private CommercePrice? Sale(decimal cost)
    {
        return O.MarkupPercent.HasValue &&
               O.MarkupFixedMinor.HasValue &&
               !string.IsNullOrWhiteSpace(O.PricingVersion)
                    ? pricing.Calculate(cost, O.MarkupPercent.Value, O.MarkupFixedMinor.Value, O.PricingVersion)
                    : null;

    }

    private string? Category(TpProgramDto p)
    {
        return p.ProgramCategory != null &&
               O.CategoryCodes.TryGetValue(p.ProgramCategory.Code, out var code) &&
               !string.IsNullOrWhiteSpace(code)
                ? code
                : null;
    }

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

    private static CommerceDomainException Error(string message) =>
        new(message, "TOURIST_PANEL_VALIDATION");

    private sealed record Selection(TpProgramDto Program, TpTicketTypeDto Type, TpTicketPriceDto Price, CommerceQuoteResult Quote);
    private sealed record ReservationContext(string AccountKey, IReadOnlyList<CommerceReservedLine> Lines, TpTempShoppingCartDto Cart);

    public async Task<CommerceQuoteResult> QuoteAsync(CommerceQuoteRequest request, CancellationToken ct) =>
        (await Select(request, ct)).Quote;

    private async Task<Selection> Select(CommerceQuoteRequest request, CancellationToken ct)
    {
        if (request.Quantity is < 1 or > 100)
            throw Error("تعداد بلیط معتبر نیست");

        var p = await client.GetDetailAsync(request.ProductKey, null, ct);

        ValidateProgram(p, request.ProductKey);

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

        var matches = types.Where(t => ValidTicketType(p, t))
            .SelectMany(t => EligiblePrices(t)
            .Select(price => (Type: t, Price: price)))
            .Where(x => OptionKey(x.Type, x.Price) == request.PurchaseOptionKey)
            .ToArray();

        if (matches.Length != 1)
            throw Error("رده قیمت مجاز و یکتا یافت نشد");

        var (t, selected) = matches[0];

        if (!CanSell(p, t) || ((int)p.Type == 2 && t.RemainingCapacity < request.Quantity))
            throw Error("خرید یا ظرفیت این بلیط در دسترس نیست");

        var priceResult = Sale(selected.BuyPrice)
            ?? throw Error("قاعده قیمت تنظیم نشده است");

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
            providerCostMinor = priceResult.ProviderCostMinor,
            saleMinor = priceResult.SaleMinor,
            markupMinor = priceResult.MarkupMinor,
            pricingVersion = priceResult.Version
        });

        var quote = new CommerceQuoteResult(
            Key,
            p.SupplyChainHojreId!,
            p.Id!,
            request.OfferKey,
            request.PurchaseOptionKey,
            p.SupplyChainHojre?.Title ?? "فروشنده",
            p.Title ?? "خدمت", Image(p.IndexImage),
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
            PricingVersion = priceResult.Version,
            RequiresManifest = p.IsManifestNeeded,
            RequiresIdentityNumber = p.IsManifestUniqueNumberNeeded
        };

        return new(p, t, selected, quote);
    }

    public Task CancelAsync(CommerceCancellationRequest request, CancellationToken ct) =>
        throw Error("لغو بلیط صادرشده توریست‌پنل پشتیبانی نمی‌شود");
}
