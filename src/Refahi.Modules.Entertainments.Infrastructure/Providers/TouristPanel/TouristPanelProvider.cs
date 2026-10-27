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

public sealed partial class TouristPanelProvider(TouristPanelClient client, TouristPanelCatalog catalog,
    TouristPanelReadCache readCache, IOptions<TouristPanelOptions> options, IEntertainmentPricingService pricing, EntertainmentDbContext db, IEntertainmentSecretProtector secrets, IEntertainmentDiscovery discovery)
    : IEntertainmentProvider, IEntertainmentOfferProvider, IEntertainmentReservationProvider, IEntertainmentFulfillmentStatusProvider
{
    public string Key => "touristpanel";
    public string Name => "توریست‌پنل";
    public EntertainmentProviderCapabilities Capabilities => new(false, false);
    private TouristPanelOptions O => options.Value;
    private static readonly EntertainmentAddressDto EmptyAddress = new("", "", "", null);
    public async Task<IReadOnlyList<EntertainmentSupplierDto>> GetSuppliersAsync(CancellationToken ct)
    {
        var query = await catalog.ReadAsync(ct);

        return query.GroupBy(x => x.SupplyChainHojreId!, StringComparer.OrdinalIgnoreCase)
                    .Select(g => new EntertainmentSupplierDto(
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

    public async Task<EntertainmentSupplierDto?> GetSupplierAsync(string supplierKey, CancellationToken ct)
    {
        var query = await GetSuppliersAsync(ct);

        return query.SingleOrDefault(x => x.SupplierKey.Equals(supplierKey, StringComparison.OrdinalIgnoreCase));
    }

    public async Task<EntertainmentCatalogPage> GetProgramsAsync(EntertainmentCatalogQuery query, CancellationToken ct)
    {
        var list = await catalog.ReadAsync(ct);

        var matchingKeys =
            (await discovery.SearchAsync(new(query.Search, null, null), int.MaxValue, ct)).Items.Select(x => x.ProgramKey.ToString("N")).ToHashSet();
        var rows = list.Where(x => x.IsActive && (int)x.Type is 1 or 2)
                       .Where(x => string.IsNullOrWhiteSpace(query.SupplierKey) || string.Equals(query.SupplierKey, x.SupplyChainHojreId, StringComparison.OrdinalIgnoreCase))
                       .Where(x => string.IsNullOrWhiteSpace(query.LocationCode) || x.LocationData?.Code == query.LocationCode)
                       .Where(x => string.IsNullOrWhiteSpace(query.CategoryCode) || x.ProgramCategory?.Code.ToString() == query.CategoryCode)
                       .Where(x => matchingKeys.Contains(x.Id!))
                        .OrderBy(x => Refahi.Modules.Catalogue.Application.Contracts.CatalogueText.Normalize(x.Title), Refahi.Modules.Catalogue.Application.Contracts.CatalogueText.Comparer).ThenBy(x => x.Id, StringComparer.Ordinal).ToArray();

        var items = new List<EntertainmentProgramDto>();
        foreach (var row in rows.Skip((query.PageNumber - 1) * query.PageSize).Take(query.PageSize))
        {
            var profile = await discovery.ProfileAsync(Guid.Parse(row.Id!), ct);
            if (profile is not null) items.Add(profile);
        }
        return new(items, query.PageNumber, query.PageSize, rows.Length);
    }
    public async Task<EntertainmentProgramDto?> GetProgramAsync(string programKey, CancellationToken ct)
        => Guid.TryParse(programKey, out var id) ? await discovery.ProfileAsync(id, ct) : null;
    public async Task<EntertainmentCatalogFilters> GetFiltersAsync(CancellationToken ct)
    {
        var rows = await catalog.ReadAsync(ct);

        return new(
            rows.Where(x => x.LocationData?.Code != null)
                .DistinctBy(x => x.LocationData!.Code)
                .Select(x =>
                    new EntertainmentFilterOption(
                        x.LocationData!.Code!,
                        x.LocationData.Native ?? ""
                    )
                ).ToArray(),

            rows.Where(x => x.ProgramCategory != null)
                .DistinctBy(x => x.ProgramCategory!.Code)
                .Select(x =>
                    new EntertainmentFilterOption(
                        x.ProgramCategory!.Code.ToString(),
                        x.ProgramCategory.Title ?? ""
                    )
                ).ToArray()
        );
    }

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
    private EntertainmentOfferDto MakeOffer(TpProgramDto p, string key, string title, DateTimeOffset? starts, int? capacity, IReadOnlyList<TpTicketTypeDto> types)
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

    private IEnumerable<EntertainmentPurchaseOptionDto> Options(TpProgramDto program, TpTicketTypeDto type)
    {
        var prices = EligiblePrices(type).ToArray();

        return prices.Select(price =>
            new EntertainmentPurchaseOptionDto(
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

    private EntertainmentPrice? Sale(decimal cost)
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

        var quote = new EntertainmentQuoteResult(
            Key,
            p.SupplyChainHojreId!,
            Guid.Parse(request.ProgramKey).ToString("N"),
            request.OfferKey,
            request.PurchaseOptionKey,
            p.SupplyChainHojre?.Title ?? "فروشنده",
            p.Title ?? "خدمت", (await discovery.ProfileAsync(Guid.Parse(request.ProgramKey), ct))?.ImageUrl,
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

    public Task CancelAsync(EntertainmentCancellationRequest request, CancellationToken ct) =>
        throw Error("لغو بلیط صادرشده توریست‌پنل پشتیبانی نمی‌شود");
}
