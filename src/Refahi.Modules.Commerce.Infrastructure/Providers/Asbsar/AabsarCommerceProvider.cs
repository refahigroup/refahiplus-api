using System.Globalization;
using System.Text.Json;
using Refahi.Modules.Commerce.Application.Contracts.Providers;
using Refahi.Modules.Commerce.Infrastructure.Providers.Asbsar.Abstraction;
using Refahi.Modules.Commerce.Infrastructure.Providers.Asbsar.Dtos;

namespace Refahi.Modules.Commerce.Infrastructure.Providers.Asbsar;

public sealed class AabsarCommerceProvider(IAabsarApiClient api) : ICommerceProvider
{
    public string Key => "aabsar";
    public string Name => "آبسار";
    public CommerceProviderCapabilities Capabilities => new(true, false);

    public Task<IReadOnlyList<CommerceSellerDto>> GetSellersAsync(CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<CommerceSellerDto>>([new(Key, Key, "پارک‌های آبی و استخرهای آبسار", null)]);

    public async Task<CommerceCatalogPage> GetProductsAsync(CommerceCatalogQuery query, CancellationToken ct)
    {
        var rows = (await api.GetShowtimesAsync(ct)).Data ?? [];
        var products = MapProducts(rows.Where(x => x.Capacity > 0));
        if (!string.IsNullOrWhiteSpace(query.Search))
            products = products.Where(x => x.Title.Contains(query.Search.Trim(), StringComparison.OrdinalIgnoreCase)).ToList();
        var total = products.Count;
        var page = products.Skip((query.PageNumber - 1) * query.PageSize).Take(query.PageSize).ToList();
        return new CommerceCatalogPage(page, query.PageNumber, query.PageSize, total);
    }

    public async Task<CommerceProductDto?> GetProductAsync(string productKey, CancellationToken ct)
    {
        var rows = (await api.GetShowtimesAsync(ct)).Data ?? [];
        return MapProducts(rows.Where(x => x.EventId == productKey && x.Capacity > 0)).SingleOrDefault();
    }

    public async Task<CommerceQuoteResult> QuoteAsync(CommerceQuoteRequest request, CancellationToken ct)
    {
        if (request.Quantity is <= 0 or > 100) throw new InvalidOperationException("تعداد بلیط معتبر نیست");
        var product = await GetProductAsync(request.ProductKey, ct)
            ?? throw new InvalidOperationException("محصول آبسار یافت نشد");
        var offer = product.Offers.SingleOrDefault(x => x.OfferKey == request.OfferKey)
            ?? throw new InvalidOperationException("سانس آبسار یافت نشد");
        var current = (await api.CheckShowtimeAsync(new AabsarCheckShowtimeRequest { ShowtimeId = request.OfferKey }, ct)).Data
            ?? throw new InvalidOperationException("امکان استعلام سانس آبسار وجود ندارد");
        if (current.Capacity < request.Quantity || !string.Equals(current.Status, "active", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("ظرفیت سانس انتخاب‌شده کافی نیست");
        var option = offer.PurchaseOptions.SingleOrDefault(x => x.Key.Equals(request.PurchaseOptionKey, StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException("نوع بلیط معتبر نیست");
        var price = option.Key == "child" ? current.ChildPrice : current.AdultPrice;
        var payload = JsonSerializer.Serialize(new { showtime_id = request.OfferKey, event_id = request.ProductKey, ticket_type = option.Key });
        return new(Key, Key, request.ProductKey, request.OfferKey, option.Key, product.Title, offer.Title,
            option.Title, "entertainment.waterpark", request.Quantity, price, current.Capacity, payload);
    }

    public async Task<CommerceFulfillmentResult> FulfillAsync(CommerceFulfillmentRequest request, CancellationToken ct)
    {
        var items = request.Lines.GroupBy(x => x.OfferKey).Select(group => new AabsarCreateOrderItem
        {
            ShowtimeId = group.Key,
            AdultQuantity = group.Where(x => x.PurchaseOptionKey == "adult").Sum(x => x.Quantity),
            ChildQuantity = group.Where(x => x.PurchaseOptionKey == "child").Sum(x => x.Quantity)
        }).ToArray();
        try
        {
            var result = (await api.CreateOrderAsync(new AabsarCreateOrderRequest
            {
                Items = items,
                User = new AabsarCreateOrderUser { FullName = request.RecipientName.Trim(), Phone = NormalizeMobile(request.RecipientMobile) }
            }, ct)).Data ?? throw new InvalidOperationException("پاسخ صدور بلیط آبسار معتبر نیست");
            var expectedTicketCount = items.Sum(x => x.AdultQuantity + x.ChildQuantity);
            if (string.IsNullOrWhiteSpace(result.OrderCode) || result.Tickets.Count != expectedTicketCount || result.Tickets.Any(x => string.IsNullOrWhiteSpace(x.TicketCode)))
                throw new CommerceProviderAmbiguousException("پاسخ صدور بلیط آبسار ناقص است");
            return new(result.OrderCode, result.Tickets.Select(x => new CommerceIssuedTicket(x.TicketCode, x.IsChild)).ToArray());
        }
        catch (AabsarApiException ex) when ((int?)ex.StatusCode is >= 500 || ex.StatusCode == System.Net.HttpStatusCode.OK)
        { throw new CommerceProviderAmbiguousException("نتیجه صدور بلیط آبسار نامشخص است", ex); }
        catch (AabsarApiException) { throw; }
        catch (OperationCanceledException ex) when (!ct.IsCancellationRequested)
        { throw new CommerceProviderAmbiguousException("نتیجه صدور بلیط آبسار نامشخص است", ex); }
        catch (HttpRequestException ex)
        { throw new CommerceProviderAmbiguousException("نتیجه صدور بلیط آبسار نامشخص است", ex); }
    }

    public async Task CancelAsync(CommerceCancellationRequest request, CancellationToken ct)
    {
        try
        {
            await api.CancelTicketsAsync(new AabsarCancelTicketsRequest
            { OrderCode = request.ProviderOrderCode, Adult = request.AdultCount, Child = request.ChildCount }, ct);
        }
        catch (AabsarApiException ex) when ((int?)ex.StatusCode is >= 500 || ex.StatusCode == System.Net.HttpStatusCode.OK)
        { throw new CommerceProviderAmbiguousException("نتیجه لغو بلیط آبسار نامشخص است", ex); }
        catch (OperationCanceledException ex) when (!ct.IsCancellationRequested)
        { throw new CommerceProviderAmbiguousException("نتیجه لغو بلیط آبسار نامشخص است", ex); }
        catch (HttpRequestException ex)
        { throw new CommerceProviderAmbiguousException("نتیجه لغو بلیط آبسار نامشخص است", ex); }
    }

    private List<CommerceProductDto> MapProducts(IEnumerable<AabsarShowtimeDto> rows) => rows
        .GroupBy(x => x.EventId)
        .Select(group => new CommerceProductDto(Key, Key, group.Key, group.First().EventTitle ?? "بلیط مجموعه آبی", null, null,
            group.OrderBy(x => x.Time).Select(MapOffer).ToArray())).ToList();

    private CommerceOfferDto MapOffer(AabsarShowtimeDto x)
    {
        var gender = x.Gender == "female" ? "بانوان" : "آقایان";
        return new(Key, Key, x.EventId, x.Id, $"{x.Title} - {gender}", x.VendorName,
            DateTimeOffset.FromUnixTimeMilliseconds(x.Time), x.Capacity, "entertainment.waterpark",
            [new("adult", "بزرگسال", x.AdultPrice, x.AdultOldPrice, x.AdultPrice > 0),
             new("child", "کودک", x.ChildPrice, x.ChildOldPrice, x.ChildPrice > 0)]);
    }

    private static string NormalizeMobile(string value)
    {
        var digits = new string(value.Where(char.IsDigit).ToArray());
        if (digits.StartsWith("+98", StringComparison.Ordinal)) digits = digits[3..];
        if (digits.StartsWith("98", StringComparison.Ordinal) && digits.Length == 12) digits = digits[2..];
        if (digits.StartsWith('0')) digits = digits[1..];
        return digits;
    }
}
