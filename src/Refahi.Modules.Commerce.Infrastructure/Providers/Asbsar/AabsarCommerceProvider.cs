using System.Globalization;
using System.Text.Json;
using Refahi.Modules.Commerce.Application.Contracts.Providers;
using Refahi.Modules.Commerce.Application.Contracts.Providers.Dtos;
using Refahi.Modules.Commerce.Application.Contracts.Providers.Exceptions;
using Refahi.Modules.Commerce.Application.Contracts.Providers.Requests;
using Refahi.Modules.Commerce.Infrastructure.Providers.Asbsar.Abstraction;
using Refahi.Modules.Commerce.Infrastructure.Providers.Asbsar.Dtos;

namespace Refahi.Modules.Commerce.Infrastructure.Providers.Asbsar;

public sealed class AabsarCommerceProvider(IAabsarApiClient api) : ICommerceProvider
{
    public string Key => "aabsar";
    public string Name => "مجموعه آبی آبسار";
    public CommerceProviderCapabilities Capabilities => new(true, false);

    private CommerceAddressDto Address =>
        new CommerceAddressDto("اصفهان", "اصفهان", "اصفهان سپاهان شهر بلوار شاهد مجموعه آبی آبسار", new LocationDto(32.555924, 51.671920));

    public async Task<IReadOnlyList<CommerceSellerDto>> GetSellersAsync(CancellationToken ct)
    {
        return [
            new CommerceSellerDto(
                Key,
                Key,
                "مجموعه آبی آبسار",
                ["استخر", "تفریحات آبی", "پارک آبی"],
                "https://aabsar.com/images/aabsar-without-slogan.png",
                "https://api.aabsar.com/storage/galleries/POKOFBdFz4BVLGel0ql4GjE1tf2UbAuUSzd0ZPKF.jpg",
                 @"
مجموعه آبسار یکی از کامل‌ترین مراکز تفریحی آبی اصفهان است؛ جایی که شنا، هیجان، آرامش و تفریح خانوادگی در کنار هم قرار گرفته‌اند.
آبسار با ترکیب استخر سرپوشیده، پارک آبی، مجموعه سونا و جکوزی، فضای ماساژ، بخش‌های ویژه کودکان و مجموعه‌ای از خدمات رفاهی، محیطی را فراهم کرده است تا هر عضو خانواده، با هر سلیقه‌ای، تجربه‌ای متفاوت داشته باشد.
طراحی منحصربه‌فرد مجموعه، نور طبیعی، فضای سبز داخلی و معماری متفاوت، آبسار را از یک استخر یا پارک آبی معمولی فراتر برده و فضایی ساخته که می‌توان ساعت‌ها در آن از تفریح و استراحت لذت برد.
",
                 Address
            )
        ];
    }

    public async Task<CommerceSellerDto?> GetSellerAsync(string sellerKey, CancellationToken ct)
        => (await GetSellersAsync(ct)).SingleOrDefault(x =>
            x.SellerKey.Equals(sellerKey.Trim(), StringComparison.OrdinalIgnoreCase));

    public async Task<CommerceCatalogPage> GetProductsAsync(CommerceCatalogQuery query, CancellationToken ct)
    {
        var rows = (await api.GetShowtimesAsync(ct)).Data ?? [];
        var products = MapProducts(rows.Where(x => x.Capacity > 0));

        if (!string.IsNullOrWhiteSpace(query.SellerKey)
            && !query.SellerKey.Equals(Key, StringComparison.OrdinalIgnoreCase))
            products = [];

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
        var originalPrice = option.Key == "child" ? current.ChildOldPrice : current.AdultOldPrice;
        return new(Key, Key, request.ProductKey, request.OfferKey, option.Key, "مجموعه آبی آبسار",
            product.Title, product.ImageUrl, offer.Title, option.Title, "entertainment.waterpark",
            request.Quantity, price, originalPrice is > 0 ? originalPrice.Value : price,
            current.Capacity, payload);
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
        .Select(group => new CommerceProductDto(
            Key,
            Key,
            group.Key,
            group.First().EventTitle ?? "بلیط استفاده از مجموعه آبی",
            "",
            "https://api.aabsar.com/storage/events/aOyvNL47wj8EXqab8U1Dnxh9X1tl9c1ASiTKTtsj.jpg",
            Enumerable.Empty<string>(),
            Address,
            group.OrderBy(x => x.Time).Select(MapOffer).ToArray()
        )).ToList();

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
