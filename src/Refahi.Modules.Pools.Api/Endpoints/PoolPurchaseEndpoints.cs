using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Refahi.Modules.Pools.Application.Contracts.Public;
using Refahi.Shared.Presentation;
using static Refahi.Modules.Pools.Api.Endpoints.PoolEndpointResponse;
namespace Refahi.Modules.Pools.Api.Endpoints;
public sealed record PurchaseRequest(IReadOnlyList<PoolChoice> Choices, long ExpectedTotalMinor, string IdempotencyKey);
public sealed record CheckoutRequest(long Version, long TotalMinor);
public sealed class PoolPurchaseEndpoints : IEndpoint
{
    public void Map(object app)
    {
        if (app is not IEndpointRouteBuilder routes) return;
        Named(routes.MapPost("/purchases", (PurchaseRequest r, HttpContext h, IMediator m, CancellationToken ct) =>
        { NoStore(h); return Run(() => m.Send(new CreatePoolPurchaseCommand(User(h), r.Choices, r.ExpectedTotalMinor, (r.IdempotencyKey ?? "").Trim()), ct)); }), "Purchases.Create").RequireAuthorization("UserOrAdmin");
        Named(routes.MapGet("/purchases/{id:guid}", (Guid id, HttpContext h, IMediator m, CancellationToken ct) =>
        { NoStore(h); return Run(() => m.Send(new GetPoolPurchaseQuery(id, User(h)), ct)); }), "Purchases.Get").RequireAuthorization("UserOrAdmin");
        Named(routes.MapPost("/purchases/{id:guid}/checkout", (Guid id, CheckoutRequest r, HttpContext h, IMediator m, CancellationToken ct) =>
        { NoStore(h); return Run(() => m.Send(new CheckoutPoolPurchaseCommand(id, User(h), r.Version, r.TotalMinor), ct)); }), "Purchases.Checkout").RequireAuthorization("UserOrAdmin");
        Named(routes.MapPost("/purchases/{id:guid}/cancel", (Guid id, HttpContext h, IMediator m, CancellationToken ct) =>
        { NoStore(h); return Run(() => m.Send(new CancelPoolPurchaseCommand(id, User(h)), ct)); }), "Purchases.Cancel").RequireAuthorization("UserOrAdmin");
        Named(routes.MapGet("/purchases/{id:guid}/vouchers.pdf", async (Guid id, HttpContext h, IMediator m, CancellationToken ct) =>
        {
            NoStore(h);
            try { return Results.File(await m.Send(new DownloadPoolVouchersQuery(id, User(h)), ct), "application/pdf", $"pool-vouchers-{id:N}.pdf"); }
            catch (Refahi.Modules.Pools.Application.Contracts.PoolException ex) { return Error(ex); }
            catch (FluentValidation.ValidationException ex) { return Results.Json(ApiResponseHelper.ValidationError(ex.Errors.GroupBy(x=>x.PropertyName).ToDictionary(x=>x.Key,x=>x.Select(y=>y.ErrorMessage).ToArray())),statusCode:400); }
        }), "Purchases.Pdf").RequireAuthorization("UserOrAdmin");
    }
}
