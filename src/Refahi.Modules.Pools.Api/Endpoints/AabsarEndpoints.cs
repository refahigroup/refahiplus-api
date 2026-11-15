using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Refahi.Modules.Pools.Application.Contracts;
using Refahi.Modules.Pools.Application.Contracts.Public;
using Refahi.Modules.Pools.Application.Features.Purchases;
using Refahi.Modules.Pools.Domain;
using Refahi.Shared.Presentation;
using static Refahi.Modules.Pools.Api.Endpoints.PoolEndpointResponse;

namespace Refahi.Modules.Pools.Api.Endpoints;

public sealed class AabsarEndpoints : IEndpoint
{
    public void Map(object app)
    {
        if (app is not IEndpointRouteBuilder routes) return;
        var root = routes.MapGroup("/aabsar");
        var catalog = root.MapGroup("/catalog").AllowAnonymous();

        Named(catalog.MapGet("/sellers", (IAabsarCatalogue c, CancellationToken ct) => Run(async () =>
        {
            var venue = await c.VenueAsync(ct);
            return venue is null ? new AabsarPage<AabsarSeller>([], 1, 1, 0, 0) : new([venue], 1, 1, 1, 1);
        })), "Aabsar.Sellers");
        Named(catalog.MapGet("/sellers/{provider}/{seller}", (string provider, string seller, IAabsarCatalogue c, CancellationToken ct)
            => Run(async () => provider == "aabsar" && seller == "aabsar" && await c.VenueAsync(ct) is { } v ? v : throw Missing())),
            "Aabsar.Seller");
        Named(catalog.MapGet("/products", (string? search, int? pageNumber, int? pageSize, IAabsarCatalogue c, CancellationToken ct)
            => Run(() => c.ProductsAsync(search?.Length > 200 ? throw new PoolException("عبارت جست‌وجو طولانی است", 400) : search,
                Math.Clamp(pageNumber ?? 1, 1, 10000), Math.Clamp(pageSize ?? 20, 1, 100), ct))), "Aabsar.Products");
        Named(catalog.MapGet("/products/{provider}/{id}", (string provider, string id, IAabsarCatalogue c, CancellationToken ct)
            => Run(() => ProductAsync(c, provider, id, ct))), "Aabsar.Product");
        Named(catalog.MapGet("/products/{provider}/{id}/offers", (string provider, string id, DateOnly? start, DateOnly? end,
            IAabsarCatalogue c, CancellationToken ct) => Run(async () =>
        {
            var p = await ProductAsync(c, provider, id, ct);
            if (start.HasValue != end.HasValue || start is { } from && end is { } to && (to < from || to.DayNumber - from.DayNumber > 6))
                throw new PoolException("بازه انتخاب سانس باید حداکثر هفت روز باشد", 400);
            if (!start.HasValue) return p.Offers;
            return p.Offers.Where(x =>
            {
                var day = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(x.StartsAt, Tehran).DateTime);
                return day >= start && day <= end;
            }).ToArray();
        })), "Aabsar.Offers");
        Named(catalog.MapGet("/filters/{provider?}", () => Results.Ok(ApiResponseHelper.Success(new { Locations = Array.Empty<object>(),
            Categories = Array.Empty<object>() }))), "Aabsar.Filters");
        Named(catalog.MapGet("/legacy-products/{key}", (string key, IAabsarCatalogue c, CancellationToken ct)
            => Run(async () => await c.ResolveLegacyProductAsync(key, ct) ?? throw Missing())), "Aabsar.LegacyProduct");

        var purchase = root.MapGroup("").RequireAuthorization("UserOrAdmin");
        purchase.AddEndpointFilter(async (context, next) =>
        {
            NoStore(context.HttpContext);
            return await next(context);
        });
        Named(purchase.MapGet("/cart", (HttpContext h, AabsarCheckout c, CancellationToken ct)
            => Run(() => c.CartAsync(User(h), ct))), "Aabsar.Cart");
        Named(purchase.MapPost("/cart/items", (HttpContext h, AabsarCartItemInput input, AabsarCheckout c, CancellationToken ct)
            => Run(() => c.AddAsync(User(h), input, ct))), "Aabsar.AddCartItem");
        Named(purchase.MapPatch("/cart/items/{id:guid}", (HttpContext h, Guid id, QuantityInput input, AabsarCheckout c, CancellationToken ct)
            => Run(() => c.UpdateAsync(User(h), id, input.Quantity, ct))), "Aabsar.UpdateCartItem");
        Named(purchase.MapDelete("/cart/items/{id:guid}", (HttpContext h, Guid id, AabsarCheckout c, CancellationToken ct)
            => Run(() => c.UpdateAsync(User(h), id, null, ct))), "Aabsar.RemoveCartItem");
        Named(purchase.MapGet("/checkout/contact", (HttpContext h, AabsarCheckout c, CancellationToken ct)
            => Run(() => c.ContactAsync(User(h), ct))), "Aabsar.Contact");
        Named(purchase.MapGet("/checkout/sessions/by-key", (HttpContext h, string key, AabsarCheckout c, CancellationToken ct)
            => Run(async () => await c.FindAsync(User(h), key, ct) ?? throw Missing())), "Aabsar.FindReview");
        Named(purchase.MapPost("/checkout/sessions", (HttpContext h, AabsarCheckoutInput input, AabsarCheckout c, CancellationToken ct)
            => Run(() => c.StartAsync(User(h), input, ct))), "Aabsar.StartReview");
        Named(purchase.MapGet("/checkout/sessions/{id:guid}", (HttpContext h, Guid id, AabsarCheckout c, CancellationToken ct)
            => Run(() => c.GetAsync(User(h), id, ct))), "Aabsar.Review");
        Named(purchase.MapPost("/checkout/sessions/{id:guid}/confirm", (HttpContext h, Guid id, AabsarConfirmInput input,
            AabsarCheckout c, CancellationToken ct) => Run(() => c.ConfirmAsync(User(h), id, input.Version, ct))), "Aabsar.Confirm");
        Named(purchase.MapDelete("/checkout/sessions/{id:guid}", (HttpContext h, Guid id, AabsarCheckout c, CancellationToken ct)
            => Run(async () =>
            {
                await c.ReleaseAsync(User(h), id, ct);
                return true;
            })), "Aabsar.ReleaseReview");
        Named(purchase.MapGet("/purchases/{id:guid}", (HttpContext h, Guid id, bool? revealTickets,
            AabsarCheckout c, AabsarLifecycle lifecycle, CancellationToken ct) => Run(async () =>
                await lifecycle.OrderViewAsync(await c.OwnedAsync(User(h), id, ct), revealTickets == true, ct))), "Aabsar.Purchase");
        Named(purchase.MapPost("/purchases/{id:guid}/cancel", (HttpContext h, Guid id, AabsarCheckout c,
            AabsarLifecycle lifecycle, ISender m, CancellationToken ct) => Run(async () =>
            {
                var user = User(h);
                await c.OwnedAsync(user, id, ct);
                await m.Send(new CancelPoolPurchaseCommand(id, user), ct);
                return await lifecycle.OrderViewAsync(await c.OwnedAsync(user, id, ct), false, ct);
            })), "Aabsar.Cancel");
        Named(root.MapGet("/admin/operations", (HttpContext h, int? limit, IAabsarRepository store, CancellationToken ct)
            => Run(async () =>
            {
                NoStore(h);
                return (await store.UnresolvedAsync(Math.Clamp(limit ?? 25, 1, 100), ct))
                    .Select(x => new { x.PurchaseId, Status = x.Status.ToString(), x.IssueFingerprint, x.CancelFingerprint }).ToArray();
            })).RequireAuthorization("AdminOnly"), "Aabsar.Operations");
        Named(root.MapPost("/admin/operations/{id:guid}/resolve", (HttpContext h, Guid id, AabsarResolution input,
            AabsarLifecycle lifecycle, IPoolPurchaseRepository repo, IPoolMutationLock gate, CancellationToken ct)
            => Run(async () =>
            {
                NoStore(h);
                await using var lease = await gate.AcquireAsync(id, ct);
                var p = await repo.GetAsync(id, ct);
                if (p is null || !p.IsAabsar) throw Missing();
                await lifecycle.ResolveAsync(p, input, ct);
                return true;
            })).RequireAuthorization("AdminOnly"), "Aabsar.Resolve");
    }

    private static readonly TimeZoneInfo Tehran = TimeZoneInfo.FindSystemTimeZoneById("Asia/Tehran");
    private static PoolException Missing() => new("آیتم یافت نشد", 404);
    private static async Task<AabsarProductView> ProductAsync(IAabsarCatalogue c, string provider, string id, CancellationToken ct)
        => provider == "aabsar" && await c.ProductAsync(id, ct) is { } p ? p : throw Missing();
    public sealed record QuantityInput(int Quantity);
}
