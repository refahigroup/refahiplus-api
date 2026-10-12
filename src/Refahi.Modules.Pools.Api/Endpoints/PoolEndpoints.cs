using System.Security.Claims;
using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Refahi.Modules.Pools.Application.Contracts;
using Refahi.Shared.Presentation;

namespace Refahi.Modules.Pools.Api.Endpoints;
public sealed record PoolReserveRequest(PoolSelection Selection, long ExpectedTotalMinor, string IdempotencyKey);
public sealed record PoolCheckoutRequest(long Version, long TotalMinor);
public sealed class PoolEndpoints : IEndpoint
{
    public void Map(object app)
    {
        if (app is not IEndpointRouteBuilder routes) return;
        Public(routes.MapGet("/providers", (IMediator m, CancellationToken ct) => Run(() => m.Send(new PoolProvidersQuery(), ct))), "Providers");
        Public(routes.MapGet("/{provider}/states", (string provider, bool? activeOnly, IMediator m, CancellationToken ct) => Run(() => m.Send(new PoolStatesQuery(provider, activeOnly ?? true), ct))), "States");
        Public(routes.MapGet("/{provider}/cities", (string provider, string? stateId, bool? activeOnly, IMediator m, CancellationToken ct) => Run(() => m.Send(new PoolCitiesQuery(provider, stateId, activeOnly ?? true), ct))), "Cities");
        Public(routes.MapGet("/{provider}/pools", (string provider, string? stateId, string? cityId, string? areaId, decimal? latitude, decimal? longitude,
            string? search, int? gender, string? sort, int? page, int? limit, bool? activeOnly, IMediator m, CancellationToken ct) =>
            Run(() => m.Send(new PoolSearchQuery(provider, stateId, cityId, areaId, latitude, longitude, search, gender, sort, page ?? 1, limit ?? 10, activeOnly), ct))), "Search");
        Public(routes.MapGet("/{provider}/pools/{poolId}", (string provider, string poolId, IMediator m, CancellationToken ct) => Run(() => m.Send(new PoolDetailsQuery(provider, poolId), ct))), "Details");
        Public(routes.MapGet("/{provider}/pools/{poolId}/sessions", (string provider, string poolId, IMediator m, CancellationToken ct) => Run(() => m.Send(new PoolSessionsQuery(provider, poolId), ct))), "Sessions");
        Public(routes.MapGet("/{provider}/pools/{poolId}/terms", (string provider, string poolId, IMediator m, CancellationToken ct) => Run(() => m.Send(new PoolTermsQuery(provider, poolId), ct))), "Terms");
        Public(routes.MapPost("/{provider}/quotes", (string provider, PoolSelection r, HttpContext h, IMediator m, CancellationToken ct) =>
        { NoStore(h); return Run(() => m.Send(new PoolQuoteQuery(r with { ProviderKey = provider }), ct)); }), "Quote");
        Private(routes.MapPost("/bookings", (PoolReserveRequest r, HttpContext h, IMediator m, CancellationToken ct) =>
        { NoStore(h); return Run(() => m.Send(new ReservePoolBookingCommand(User(h), r.Selection, r.ExpectedTotalMinor, (r.IdempotencyKey ?? "").Trim()), ct)); }), "Reserve");
        Private(routes.MapGet("/bookings/{id:guid}", (Guid id, HttpContext h, IMediator m, CancellationToken ct) =>
        { NoStore(h); return Run(() => m.Send(new GetPoolBookingQuery(id, User(h)), ct)); }), "Booking");
        Private(routes.MapPost("/bookings/{id:guid}/checkout", (Guid id, PoolCheckoutRequest r, HttpContext h, IMediator m, CancellationToken ct) =>
        { NoStore(h); return Run(() => m.Send(new CheckoutPoolBookingCommand(id, User(h), r.Version, r.TotalMinor), ct)); }), "Checkout");
        Private(routes.MapPost("/bookings/{id:guid}/cancel", (Guid id, HttpContext h, IMediator m, CancellationToken ct) =>
        { NoStore(h); return Run(() => m.Send(new CancelPoolBookingCommand(id, User(h)), ct)); }), "Cancel");
    }
    private static void Public(RouteHandlerBuilder b, string name) => b.WithName("Pools." + name).WithTags("Pools").AllowAnonymous().Produces(200).Produces(400).Produces(502).Produces(503);
    private static void Private(RouteHandlerBuilder b, string name) => b.WithName("Pools." + name).WithTags("Pools.Bookings").RequireAuthorization("UserOrAdmin").Produces(200).Produces(401).Produces(403).Produces(409).Produces(503);
    private static void NoStore(HttpContext h) => h.Response.Headers.CacheControl = "private, no-store";
    private static Guid User(HttpContext h) => Guid.TryParse(h.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? h.User.FindFirstValue("sub"), out var id) && id != Guid.Empty
        ? id : throw new PoolException("ابتدا وارد حساب کاربری شوید", 401);
    private static async Task<IResult> Run<T>(Func<Task<T>> work)
    {
        try { return Results.Ok(ApiResponseHelper.Success(await work())); }
        catch (PoolException ex) { return Results.Json(ApiResponseHelper.Error(ex.Message, statusCode: ex.Status), statusCode: ex.Status); }
        catch (ValidationException ex) { return Results.Json(ApiResponseHelper.ValidationError(ex.Errors.GroupBy(x => x.PropertyName).ToDictionary(x => x.Key, x => x.Select(e => e.ErrorMessage).ToArray())), statusCode: 400); }
    }
}
