using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using MediatR;
using Microsoft.Extensions.Caching.Memory;
using Refahi.Modules.Store.Application.Contracts.Commands.Cart;
using Refahi.Modules.Store.Application.Contracts.Queries.Cart;
using Refahi.Modules.Store.Application.Services;
using Refahi.Modules.Store.Domain.Aggregates;
using Refahi.Modules.Store.Domain.Entities;
using Refahi.Modules.Store.Domain.Exceptions;
using Refahi.Modules.Store.Domain.Repositories;
using CartAggregate = Refahi.Modules.Store.Domain.Aggregates.Cart;

namespace Refahi.Modules.Store.Application.Features.Cart.OfferCartV3;

public sealed class UpdateOfferCartItemCommandHandler(
    ICartRepository carts, IOfferRepository offers, IOnlineOfferEligibilityService eligibility,
    IMediator mediator, TimeProvider clock)
    : IRequestHandler<UpdateOfferCartItemCommand, OfferCartDto>
{
    public async Task<OfferCartDto> Handle(UpdateOfferCartItemCommand request, CancellationToken ct)
    {
        var cart = await OfferCartV3Support.GetOwnedCartAsync(carts, request.UserId, request.ModuleId, ct);
        var item = cart.Items.FirstOrDefault(x => x.Id == request.CartItemId)
            ?? throw new StoreDomainException("آیتم سبد خرید یافت نشد", "CART_ITEM_NOT_FOUND");
        if (!item.OfferId.HasValue)
            throw new StoreDomainException("آیتم قدیمی در عملیات سبد v3 پشتیبانی نمی‌شود", "LEGACY_CART_ITEM_NOT_SUPPORTED");

        var current = await offers.ResolveAsync(item.ProductId, item.ShopId, item.VariantId,
            item.SessionId, clock.GetUtcNow(), ct);
        var changed = OfferCartV3Support.HasChanged(item, current);
        if (changed && !request.AcceptOfferChanges)
            throw OfferCartV3Support.Changed(item, current);
        if (current is null)
            throw OfferCartV3Support.Changed(item, null);

        var context = await eligibility.ResolveByIdAsync(current.Id, request.Quantity,
            item.VariantId, item.SessionId, item.UsageDate, ct);
        cart.RefreshOfferItem(item.Id, context.Offer.Id, context.Product.Id, context.Shop.Id,
            context.Offer.ProductVariantId, context.Offer.ProductSessionId, context.UsageDate,
            request.Quantity, context.Offer.OriginalPriceMinor, context.Offer.FinalPriceMinor);
        await carts.UpdateAsync(cart, ct);
        return await OfferCartV3Support.ProjectAsync(mediator, request.UserId, request.ModuleId, ct);
    }
}

public sealed class RemoveOfferCartItemCommandHandler(ICartRepository carts, IMediator mediator)
    : IRequestHandler<RemoveOfferCartItemCommand, OfferCartDto>
{
    public async Task<OfferCartDto> Handle(RemoveOfferCartItemCommand request, CancellationToken ct)
    {
        var cart = await OfferCartV3Support.GetOwnedCartAsync(carts, request.UserId, request.ModuleId, ct);
        var item = cart.Items.FirstOrDefault(x => x.Id == request.CartItemId)
            ?? throw new StoreDomainException("آیتم سبد خرید یافت نشد", "CART_ITEM_NOT_FOUND");
        if (!item.OfferId.HasValue)
            throw new StoreDomainException("آیتم قدیمی در عملیات سبد v3 پشتیبانی نمی‌شود", "LEGACY_CART_ITEM_NOT_SUPPORTED");
        cart.RemoveItem(item.Id);
        await carts.UpdateAsync(cart, ct);
        return await OfferCartV3Support.ProjectAsync(mediator, request.UserId, request.ModuleId, ct);
    }
}

public sealed class ReconfirmOfferCartCommandHandler(
    ICartRepository carts, IOfferRepository offers, IOnlineOfferEligibilityService eligibility,
    IMediator mediator, TimeProvider clock)
    : IRequestHandler<ReconfirmOfferCartCommand, OfferCartDto>
{
    public async Task<OfferCartDto> Handle(ReconfirmOfferCartCommand request, CancellationToken ct)
    {
        var cart = await OfferCartV3Support.GetOwnedCartAsync(carts, request.UserId, request.ModuleId, ct);
        if (cart.Items.Count == 0)
            throw new StoreDomainException("سبد خرید خالی است", "CART_EMPTY");
        if (cart.Items.Any(x => !x.OfferId.HasValue))
            throw new StoreDomainException("سبد خرید شامل آیتم قدیمی است؛ سبد v3 را دوباره ایجاد کنید",
                "LEGACY_CART_ITEM_NOT_SUPPORTED");

        var resolved = new List<(CartItem Item, OnlineOfferContext Context)>();
        var unavailable = new List<CartOfferChangedDetail>();
        foreach (var item in cart.Items)
        {
            var current = await offers.ResolveAsync(item.ProductId, item.ShopId, item.VariantId,
                item.SessionId, clock.GetUtcNow(), ct);
            if (current is null)
            {
                unavailable.Add(OfferCartV3Support.Changed(item, null).Details.Single());
                continue;
            }
            var context = await eligibility.ResolveByIdAsync(current.Id, item.Quantity,
                item.VariantId, item.SessionId, item.UsageDate, ct);
            resolved.Add((item, context));
        }
        if (unavailable.Count > 0) throw new CartOfferChangedException(unavailable);

        foreach (var (item, context) in resolved)
            cart.RefreshOfferItem(item.Id, context.Offer.Id, context.Product.Id, context.Shop.Id,
                context.Offer.ProductVariantId, context.Offer.ProductSessionId, context.UsageDate,
                item.Quantity, context.Offer.OriginalPriceMinor, context.Offer.FinalPriceMinor);
        await carts.UpdateAsync(cart, ct);
        return await OfferCartV3Support.ProjectAsync(mediator, request.UserId, request.ModuleId, ct);
    }
}

public sealed class SyncOfferCartCommandHandler(
    ICartRepository carts, IOfferRepository offers, IOnlineOfferEligibilityService eligibility,
    IMediator mediator, IMemoryCache cache, TimeProvider clock)
    : IRequestHandler<SyncOfferCartCommand, OfferCartDto>
{
    private static readonly ConcurrentDictionary<string, SemaphoreSlim> Locks = new();

    public async Task<OfferCartDto> Handle(SyncOfferCartCommand request, CancellationToken ct)
    {
        var cacheKey = $"sync_offer_cart_v3:{request.UserId:N}:{request.ModuleId}:{request.IdempotencyKey}";
        var fingerprint = Fingerprint(request);
        var gate = Locks.GetOrAdd(cacheKey, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(ct);
        try
        {
            if (cache.TryGetValue<SyncCacheEntry>(cacheKey, out var cached) && cached is not null)
            {
                if (!CryptographicOperations.FixedTimeEquals(
                    Encoding.ASCII.GetBytes(cached.Fingerprint), Encoding.ASCII.GetBytes(fingerprint)))
                    throw new StoreDomainException("کلید همگام‌سازی قبلاً با محتوای دیگری استفاده شده است",
                        "IDEMPOTENCY_PAYLOAD_MISMATCH");
                return cached.Cart;
            }

            var cart = await carts.GetByUserAndModuleIdAsync(request.UserId, request.ModuleId, ct);
            if (cart?.Items.Any(x => !x.OfferId.HasValue) == true)
                throw new StoreDomainException("سبد خرید شامل آیتم قدیمی است؛ سبد v3 را دوباره ایجاد کنید",
                    "LEGACY_CART_ITEM_NOT_SUPPORTED");
            var resolved = new List<OnlineOfferContext>();
            var quantities = new List<int>();
            var grouped = request.Items
                .GroupBy(x => new { x.OfferId, x.ProductVariantId, x.ProductSessionId, x.UsageDate,
                    x.SnapshotOriginalUnitPriceMinor, x.SnapshotFinalUnitPriceMinor })
                .Select(g => (Item: g.First(), Quantity: checked(g.Sum(x => x.Quantity))))
                .ToList();
            var drifts = new List<CartOfferChangedDetail>();

            foreach (var tuple in grouped)
            {
                var input = tuple.Item;
                var referenced = await offers.GetByIdAsync(input.OfferId, includeDeleted: true, ct);
                Offer? current = referenced is null ? null : await offers.ResolveAsync(referenced.ProductId,
                    referenced.ShopId, input.ProductVariantId, input.ProductSessionId, clock.GetUtcNow(), ct);
                var changed = current is null || current.Id != input.OfferId ||
                    current.OriginalPriceMinor != input.SnapshotOriginalUnitPriceMinor ||
                    current.FinalPriceMinor != input.SnapshotFinalUnitPriceMinor;
                if (changed && !request.AcceptOfferChanges)
                {
                    drifts.Add(new CartOfferChangedDetail(null, input.OfferId,
                        input.SnapshotOriginalUnitPriceMinor, input.SnapshotFinalUnitPriceMinor,
                        current?.Id, current?.OriginalPriceMinor, current?.FinalPriceMinor,
                        current is null ? "پیشنهاد منقضی یا ناموجود است" : "پیشنهاد یا قیمت تغییر کرده است"));
                    continue;
                }
                if (current is null)
                {
                    drifts.Add(new CartOfferChangedDetail(null, input.OfferId,
                        input.SnapshotOriginalUnitPriceMinor, input.SnapshotFinalUnitPriceMinor,
                        null, null, null, "پیشنهاد منقضی یا ناموجود است"));
                    continue;
                }
                var context = await eligibility.ResolveByIdAsync(current.Id, tuple.Quantity,
                    input.ProductVariantId, input.ProductSessionId, input.UsageDate, ct);
                resolved.Add(context);
                quantities.Add(tuple.Quantity);
            }

            if (drifts.Count > 0) throw new CartOfferChangedException(drifts);
            var shops = resolved.Select(x => x.Shop.Id)
                .Concat(cart?.Items.Where(x => x.OfferId.HasValue).Select(x => x.ShopId) ?? [])
                .Distinct().ToArray();
            if (shops.Length > 1)
                throw new StoreDomainException("تمامی محصولات باید از یک فروشگاه باشند", "MIXED_SHOP_ITEMS");

            cart ??= CartAggregate.Create(request.UserId, request.ModuleId);
            for (var i = 0; i < resolved.Count; i++)
            {
                var context = resolved[i];
                cart.AddOfferItem(context.Shop.Id, context.Product.Id, context.Offer.Id,
                    context.Offer.ProductVariantId, context.Offer.ProductSessionId, context.UsageDate,
                    quantities[i], context.Offer.OriginalPriceMinor, context.Offer.FinalPriceMinor);
            }
            if (resolved.Count > 0)
            {
                if (await carts.GetByUserAndModuleIdAsync(request.UserId, request.ModuleId, ct) is null)
                    await carts.AddAsync(cart, ct);
                else
                    await carts.UpdateAsync(cart, ct);
            }
            var result = await OfferCartV3Support.ProjectAsync(mediator, request.UserId, request.ModuleId, ct);
            cache.Set(cacheKey, new SyncCacheEntry(fingerprint, result), TimeSpan.FromHours(24));
            return result;
        }
        finally
        {
            gate.Release();
        }
    }

    private static string Fingerprint(SyncOfferCartCommand request)
    {
        var canonical = new StringBuilder("store-cart-sync-v3|")
            .Append(request.ModuleId).Append('|').Append(request.AcceptOfferChanges ? '1' : '0');
        foreach (var item in request.Items.OrderBy(x => x.OfferId).ThenBy(x => x.ProductVariantId)
                     .ThenBy(x => x.ProductSessionId).ThenBy(x => x.UsageDate))
            canonical.Append('|').Append(item.OfferId.ToString("N")).Append(':').Append(item.Quantity)
                .Append(':').Append(item.ProductVariantId?.ToString("N") ?? "-")
                .Append(':').Append(item.ProductSessionId?.ToString("N") ?? "-")
                .Append(':').Append(item.UsageDate?.ToString("yyyy-MM-dd") ?? "-")
                .Append(':').Append(item.SnapshotOriginalUnitPriceMinor)
                .Append(':').Append(item.SnapshotFinalUnitPriceMinor);
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(canonical.ToString())));
    }

    private sealed record SyncCacheEntry(string Fingerprint, OfferCartDto Cart);
}

internal static class OfferCartV3Support
{
    public static async Task<CartAggregate> GetOwnedCartAsync(ICartRepository carts, Guid userId, int moduleId,
        CancellationToken ct) => await carts.GetByUserAndModuleIdAsync(userId, moduleId, ct)
        ?? throw new StoreDomainException("سبد خرید یافت نشد", "CART_NOT_FOUND");

    public static bool HasChanged(CartItem item, Offer? current) => current is null ||
        current.Id != item.OfferId || current.OriginalPriceMinor != item.OriginalUnitPriceMinor ||
        current.FinalPriceMinor != item.UnitPriceMinor;

    public static CartOfferChangedException Changed(CartItem item, Offer? current) => new([
        new CartOfferChangedDetail(item.Id, item.OfferId!.Value, item.OriginalUnitPriceMinor,
            item.UnitPriceMinor, current?.Id, current?.OriginalPriceMinor, current?.FinalPriceMinor,
            current is null ? "پیشنهاد منقضی یا ناموجود است" : "پیشنهاد یا قیمت تغییر کرده است")]);

    public static async Task<OfferCartDto> ProjectAsync(IMediator mediator, Guid userId, int moduleId,
        CancellationToken ct) => await mediator.Send(new GetOfferCartQuery(userId, moduleId), ct)
        ?? throw new StoreDomainException("سبد خرید یافت نشد", "CART_NOT_FOUND");
}
