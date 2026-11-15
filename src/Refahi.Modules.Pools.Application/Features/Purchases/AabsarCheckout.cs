using System.Globalization;
using System.Text.Json;
using FluentValidation;
using MediatR;
using Refahi.Modules.Identity.Application.Contracts.Queries;
using Refahi.Modules.Pools.Application.Contracts;
using Refahi.Modules.Pools.Application.Contracts.Public;
using Refahi.Modules.Pools.Domain;

namespace Refahi.Modules.Pools.Application.Features.Purchases;

public sealed record AabsarCartView(Guid CartId, IReadOnlyList<AabsarCartLine> Items, long TotalAmountMinor,
    Dictionary<string, string> CheckoutVersions);
public sealed record AabsarCartLine(Guid Id, string ProviderKey, string SellerKey, string ProductKey, string OfferKey,
    string PurchaseOptionKey, string Title, string SellerTitle, string ProductTitle, string? ProductImageUrl,
    string OptionTitle, string OfferTitle, int Quantity, long UnitPriceMinor, long OriginalUnitPriceMinor, bool IsAvailable);
public sealed record AabsarReview(Guid Id, string ProviderKey, string Status, string Version, DateTimeOffset PayableUntil,
    long TotalAmountMinor, IReadOnlyList<AabsarReviewLine> Items, Guid? AabsarOrderId);
public sealed record AabsarReviewLine(Guid CartItemId, string ProductTitle, string OptionTitle, int Quantity, long UnitPriceMinor);
public sealed record AabsarCheckoutResult(Guid AabsarOrderId, Guid OrderId, string OrderNumber, long FinalAmountMinor,
    string CheckoutDestination);

public sealed class AabsarCheckout(IAabsarRepository store, IPoolPurchaseRepository purchases, IAabsarCatalogue catalogue,
    IAabsarGateway gateway, IPoolMutationLock gate, IPoolCodeProtector protector, PoolPurchaseAccess access,
    ISender sender, TimeProvider clock, IValidator<AabsarCartItemInput> cartValidator,
    IValidator<AabsarCheckoutInput> checkoutValidator)
{
    private const string Key = "aabsar";
    private static readonly TimeZoneInfo Tehran = TimeZoneInfo.FindSystemTimeZoneById("Asia/Tehran");

    public async Task<AabsarContact> ContactAsync(Guid user, CancellationToken ct)
    {
        var contact = await sender.Send(new GetUserPurchaseContactQuery(user), ct);
        return new(contact?.FullName ?? "", contact?.MobileNumber);
    }

    public async Task<AabsarCartView> CartAsync(Guid user, CancellationToken ct) => Map(await store.CartAsync(user, ct));

    public async Task<AabsarCartView> AddAsync(Guid user, AabsarCartItemInput input, CancellationToken ct)
    {
        await cartValidator.ValidateAndThrowAsync(input, ct);
        EnsureEnabled();
        await using var lease = await gate.AcquireAsync(user, ct);
        var cart = await store.CartAsync(user, ct);
        var productKey = Guid.Parse(input.ProductKey).ToString("D");
        var offerKey = Guid.Parse(input.OfferKey).ToString("D");
        var product = await catalogue.ProductAsync(productKey, ct);
        var offer = product?.Offers.SingleOrDefault(x => x.OfferKey == offerKey);
        if (product is null || offer is null) throw Unavailable();
        var row = await store.ShowtimesAsync([Guid.Parse(offer.OfferKey)], ct);
        if (!row.TryGetValue(Guid.Parse(offer.OfferKey), out var showtime) || !showtime.IsActive || showtime.StartsAt <= clock.GetUtcNow()) throw Unavailable();
        var quote = await gateway.QuoteAsync(showtime.ShowtimeId, ct);
        var other = cart?.Items.Where(x => x.OfferKey == offerKey && x.PurchaseOptionKey != input.PurchaseOptionKey)
            .Sum(x => x.Quantity) ?? 0;
        var (price, oldPrice) = Price(quote, input.PurchaseOptionKey, input.Quantity + other);
        _ = checked(price * input.Quantity);
        _ = checked(oldPrice * input.Quantity);
        if (price != input.ExpectedUnitPriceMinor) throw PriceChanged(price);
        if (cart is null) { cart = AabsarCart.Create(user); store.Add(cart); }
        var option = input.PurchaseOptionKey == "adult" ? "بزرگسال" : "کودک";
        cart.AddOrReplace(new(Key, Key, product.ProductKey, offer.OfferKey, input.PurchaseOptionKey,
            $"{product.Title} - {option}", "مجموعه آبی آبسار", product.Title, product.ImageUrl, option, offer.Title,
            input.Quantity, price, oldPrice));
        await store.SaveAsync(ct);
        return Map(cart);
    }

    public async Task<AabsarCartView> UpdateAsync(Guid user, Guid itemId, int? quantity, CancellationToken ct)
    {
        if (quantity is < 1 or > 100) throw new PoolException("تعداد بلیت معتبر نیست", 400);
        await using var lease = await gate.AcquireAsync(user, ct);
        var cart = await store.CartAsync(user, ct) ?? throw Unavailable();
        var item = cart.Items.SingleOrDefault(x => x.Id == itemId) ?? throw Unavailable();
        if (quantity.HasValue)
        {
            EnsureEnabled();
            var rows = await store.ShowtimesAsync([Guid.Parse(item.OfferKey)], ct);
            if (!rows.TryGetValue(Guid.Parse(item.OfferKey), out var row) || !row.IsActive || row.StartsAt <= clock.GetUtcNow()) throw Unavailable();
            var current = await gateway.QuoteAsync(row.ShowtimeId, ct);
            var other = cart.Items.Where(x => x.Id != itemId && x.OfferKey == item.OfferKey).Sum(x => x.Quantity);
            var (price, oldPrice) = Price(current, item.PurchaseOptionKey, quantity.Value + other);
            if (price != item.ExpectedUnitPriceMinor) throw PriceChanged(price);
            _ = checked(price * quantity.Value);
            _ = checked(oldPrice * quantity.Value);
            cart.UpdateQuantity(itemId, quantity.Value);
        }
        else cart.Remove(itemId);
        await store.SaveAsync(ct);
        return Map(cart);
    }

    public async Task<AabsarReview> StartAsync(Guid user, AabsarCheckoutInput input, CancellationToken ct)
    {
        await checkoutValidator.ValidateAndThrowAsync(input, ct);
        var key = "aabsar:" + input.IdempotencyKey.Trim();
        var fingerprint = PoolKeys.Hash(input.CartVersion!);
        await using var lease = await gate.AcquireAsync(user, ct);
        var existing = await purchases.FindAsync(user, key, ct);
        if (existing is not null)
        {
            if (existing.Fingerprint != fingerprint) throw CartChanged();
            return await ReviewAsync(existing, ct);
        }
        EnsureEnabled();
        var cart = await store.CartAsync(user, ct);
        if (cart is null || cart.Items.Count is 0 or > 100 || Version(cart) != input.CartVersion) throw CartChanged();
        var contact = await ContactAsync(user, ct);
        var digits = new string((contact.MobileNumber ?? "").Where(char.IsAsciiDigit).ToArray());
        if (digits.StartsWith("98", StringComparison.Ordinal) && digits.Length == 12) digits = digits[2..];
        digits = digits.TrimStart('0');
        if (contact.FullName.Trim().Length is < 3 or > 255 || digits.Length != 10 || digits[0] != '9')
            throw new PoolException("برای ادامه خرید، نام و شماره موبایل پروفایل خود را تکمیل کنید", 409);

        var selected = cart.Items.OrderBy(x => x.Id).ToArray();
        var rows = await store.ShowtimesAsync(selected.Select(x => Guid.Parse(x.OfferKey)), ct);
        var requests = new List<PoolRequest>();
        foreach (var group in selected.GroupBy(x => Guid.Parse(x.OfferKey)))
        {
            if (!rows.TryGetValue(group.Key, out var row) || !row.IsActive || row.StartsAt <= clock.GetUtcNow()) throw Unavailable();
            var quote = await gateway.QuoteAsync(row.ShowtimeId, ct);
            var total = group.Sum(x => x.Quantity);
            var local = TimeZoneInfo.ConvertTime(row.StartsAt, Tehran);
            foreach (var item in group)
            {
                var (price, _) = Price(quote, item.PurchaseOptionKey, total);
                requests.Add(PoolRequest.CreateAabsar(row.Id, row.ProductId, item.Title,
                    local.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), local.ToString("HH:mm", CultureInfo.InvariantCulture),
                    item.PurchaseOptionKey, item.Quantity, price, row.StartsAt));
            }
        }
        var now = clock.GetUtcNow();
        var until = requests.Min(x => x.StartsAtUtc);
        if (until > now.AddMinutes(30)) until = now.AddMinutes(30);
        if (until <= now) throw Unavailable();
        var p = PoolPurchase.Create(user, key, fingerprint, requests, now, until);
        var fulfillment = AabsarFulfillment.Create(p.Id, protector.Protect(JsonSerializer.Serialize(contact with { FullName = contact.FullName.Trim(), MobileNumber = digits })),
            JsonSerializer.Serialize(selected.Select(Line).ToArray()));
        store.Add(fulfillment);
        await purchases.AddAsync(p, ct);
        return await ReviewAsync(p, ct);
    }

    public async Task<AabsarReview?> FindAsync(Guid user, string key, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(key) || key.Length > 120) return null;
        var p = await purchases.FindAsync(user, "aabsar:" + key.Trim(), ct);
        return p is null ? null : await ReviewAsync(p, ct);
    }

    public async Task<PoolPurchase> OwnedAsync(Guid user, Guid id, CancellationToken ct)
    {
        var p = await access.Owned(id, user, ct);
        return p.IsAabsar ? p : throw new PoolException("خرید یافت نشد", 404);
    }

    public async Task<AabsarReview> GetAsync(Guid user, Guid id, CancellationToken ct)
        => await ReviewAsync(await OwnedAsync(user, id, ct), ct);

    public async Task ReleaseAsync(Guid user, Guid id, CancellationToken ct)
    {
        await using var lease = await gate.AcquireAsync(id, ct);
        var p = await OwnedAsync(user, id, ct);
        if (p.OrderId.HasValue || await sender.Send(new Refahi.Modules.Orders.Application.Contracts.Queries.GetOrderByIdempotencyKeyQuery(
            PoolKeys.Order(p.Id), p.UserId, "Pools"), ct) is not null)
            throw new PoolException("این خرید به سفارش متصل شده است", 409);
        p.BeginCancellation();
        p.CompleteCancellation(false);
        await purchases.SaveAsync(ct);
    }

    public async Task<AabsarCheckoutResult> ConfirmAsync(Guid user, Guid id, string version, CancellationToken ct)
    {
        var p = await OwnedAsync(user, id, ct);
        if (!long.TryParse(version, out var reviewed) ||
            reviewed != p.Version && !(p.OrderId.HasValue && reviewed == 1))
            throw CartChanged();
        var order = await sender.Send(new CheckoutPoolPurchaseCommand(id, user, reviewed, p.TotalMinor), ct);
        await using var lease = await gate.AcquireAsync(user, ct);
        var cart = await store.CartAsync(user, ct);
        var f = await FulfillmentAsync(id, ct);
        if (cart is not null)
        {
            var snapshot = JsonSerializer.Deserialize<AabsarCartLine[]>(f.CartSnapshot)!;
            foreach (var line in snapshot)
            {
                var current = cart.Items.SingleOrDefault(x => x.Id == line.Id);
                if (current is not null && current.OfferKey == line.OfferKey && current.PurchaseOptionKey == line.PurchaseOptionKey &&
                    current.Quantity == line.Quantity && current.ExpectedUnitPriceMinor == line.UnitPriceMinor) cart.Remove(line.Id);
            }
            await store.SaveAsync(ct);
        }
        return new(id, order.OrderId, order.OrderNumber, order.TotalMinor, $"/checkout/orders/{order.OrderId}");
    }

    public async Task<AabsarFulfillment> FulfillmentAsync(Guid id, CancellationToken ct)
        => await store.FulfillmentAsync(id, ct) ?? throw new PoolException("اطلاعات خرید آبسار یافت نشد", 404);

    private async Task<AabsarReview> ReviewAsync(PoolPurchase p, CancellationToken ct)
    {
        var f = await FulfillmentAsync(p.Id, ct);
        var snapshot = JsonSerializer.Deserialize<AabsarCartLine[]>(f.CartSnapshot)!;
        var lines = p.Requests.Select(x => new AabsarReviewLine(
            snapshot.Single(s => s.OfferKey == x.SessionId.ToString("D") && s.PurchaseOptionKey == x.TicketType).Id,
            x.PoolName, x.TicketType == "adult" ? "بزرگسال" : "کودک", x.OrderQuantity, x.OrderUnitPrice)).ToArray();
        var status = p.OrderId.HasValue ? "Confirmed" : p.Status == PoolPurchaseStatus.Cancelled ? "Released" :
            p.PayableUntil <= clock.GetUtcNow() ? "Expired" : "Reserved";
        return new(p.Id, Key, status, p.Version.ToString(CultureInfo.InvariantCulture), p.PayableUntil, p.TotalMinor, lines,
            p.OrderId.HasValue ? p.Id : null);
    }

    internal static (long Price, long OldPrice) Price(AabsarLiveQuote quote, string type, int count)
    {
        var price = type == "adult" ? quote.AdultPrice : quote.ChildPrice;
        var old = type == "adult" ? quote.AdultOldPrice : quote.ChildOldPrice;
        if (price <= 0 || count > quote.Capacity) throw Unavailable();
        return (price, old is > 0 ? old.Value : price);
    }

    private void EnsureEnabled()
    {
        if (!gateway.Options.Enabled) throw new PoolException("فروش آبسار فعال نیست", 503);
    }

    private static AabsarCartLine Line(AabsarCartItem x) => new(x.Id, x.ProviderKey, x.SellerKey, x.ProductKey, x.OfferKey,
        x.PurchaseOptionKey, x.Title, x.SellerTitle, x.ProductTitle, x.ProductImageUrl, x.OptionTitle, x.OfferTitle,
        x.Quantity, x.ExpectedUnitPriceMinor, x.OriginalUnitPriceMinor, x.IsAvailable);
    private static AabsarCartView Map(AabsarCart? cart) => cart is null ? new(Guid.Empty, [], 0, []) :
        new(cart.Id, cart.Items.Select(Line).ToArray(), cart.Items.Sum(x => checked(x.Quantity * x.ExpectedUnitPriceMinor)),
            new() { [Key] = Version(cart) });
    private static string Version(AabsarCart cart) => PoolKeys.Hash(JsonSerializer.Serialize(cart.Items.OrderBy(x => x.Id)
        .Select(x => new { x.Id, x.OfferKey, x.PurchaseOptionKey, x.Quantity, x.ExpectedUnitPriceMinor })));
    private static PoolException Unavailable() => new("سانس یا ظرفیت انتخاب‌شده در دسترس نیست", 409, "SESSION_UNAVAILABLE");
    private static PoolException CartChanged() => new("اقلام یا قیمت سبد تغییر کرده است؛ سبد را دوباره بارگذاری کنید", 409, "CART_CHANGED");
    private static PoolException PriceChanged(long price) => new("قیمت بلیت تغییر کرده است", 409, "PRICE_CHANGED", new { UnitPriceMinor = price });
}

public sealed class AabsarCheckoutInputValidator : AbstractValidator<AabsarCheckoutInput>
{
    public AabsarCheckoutInputValidator()
    {
        RuleFor(x => x.IdempotencyKey).NotEmpty().MaximumLength(120).WithMessage("کلید درخواست معتبر نیست");
        RuleFor(x => x.CartVersion).NotEmpty().MaximumLength(100).WithMessage("نسخه سبد معتبر نیست");
    }
}

public sealed class AabsarCartItemValidator : AbstractValidator<AabsarCartItemInput>
{
    public AabsarCartItemValidator()
    {
        RuleFor(x => x.ProviderKey).Equal("aabsar").WithMessage("ارائه‌دهنده معتبر نیست");
        RuleFor(x => x.SellerKey).Equal("aabsar").WithMessage("مجموعه معتبر نیست");
        RuleFor(x => x.ProductKey).Must(value => Guid.TryParse(value, out _)).WithMessage("محصول معتبر نیست");
        RuleFor(x => x.OfferKey).Must(value => Guid.TryParse(value, out _)).WithMessage("سانس معتبر نیست");
        RuleFor(x => x.PurchaseOptionKey).Must(value => value is "adult" or "child").WithMessage("نوع بلیت معتبر نیست");
        RuleFor(x => x.Quantity).InclusiveBetween(1, 100).WithMessage("تعداد بلیت معتبر نیست");
        RuleFor(x => x.ExpectedUnitPriceMinor).GreaterThan(0).WithMessage("قیمت بلیت معتبر نیست");
    }
}
