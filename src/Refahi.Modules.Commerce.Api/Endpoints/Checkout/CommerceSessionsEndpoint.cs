using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Refahi.Modules.Commerce.Api.Endpoints.Cart;
using Refahi.Modules.Commerce.Application.Features.Checkout.Sessions;
using Refahi.Modules.Commerce.Domain;
using Refahi.Shared.Presentation;

namespace Refahi.Modules.Commerce.Api.Endpoints.Checkout;

public sealed class CommerceSessionsEndpoint : IEndpoint
{
    public void Map(object app)
    {
        if (app is not IEndpointRouteBuilder routes) return;
        routes.MapGet("/checkout/sessions/by-key", async (string key, HttpContext c, IMediator m, CancellationToken ct) =>
        {
            if (!_Helpers.TryUser(c, out var user)) return _Helpers.Unauthorized();
            c.Response.Headers.CacheControl = "private, no-store";
            var value = await m.Send(new FindCommerceSessionQuery(user, key), ct);
            return value == null ? Results.NotFound(ApiResponseHelper.Error("رزرو یافت نشد", statusCode: 404)) : Results.Ok(ApiResponseHelper.Success(value));
        }).WithName("Commerce.FindSession").WithTags("Commerce.Checkout").RequireAuthorization("UserOrAdmin");
        routes.MapPost("/checkout/sessions", async (StartBody body, HttpContext c, IMediator m, CancellationToken ct) =>
        {
            if (!_Helpers.TryUser(c, out var user)) return _Helpers.Unauthorized();
            c.Response.Headers.CacheControl = "private, no-store";
            try { return Results.Ok(ApiResponseHelper.Success(await m.Send(new StartCommerceSessionCommand(user, body.ProviderKey, body.IdempotencyKey, body.Passengers ?? [], body.CartVersion), ct))); }
            catch (CommerceDomainException ex) { return Conflict(ex); }
        }).WithName("Commerce.StartSession").WithTags("Commerce.Checkout").RequireAuthorization("UserOrAdmin");
        routes.MapGet("/checkout/sessions/{id:guid}", async (Guid id, HttpContext c, IMediator m, CancellationToken ct) =>
        {
            if (!_Helpers.TryUser(c, out var user)) return _Helpers.Unauthorized();
            c.Response.Headers.CacheControl = "private, no-store";
            try { return Results.Ok(ApiResponseHelper.Success(await m.Send(new GetCommerceSessionQuery(user, id), ct))); }
            catch (CommerceDomainException ex) { return Conflict(ex); }
        }).WithName("Commerce.GetSession").WithTags("Commerce.Checkout").RequireAuthorization("UserOrAdmin");
        routes.MapPost("/checkout/sessions/{id:guid}/confirm", async (Guid id, ConfirmBody body, HttpContext c, IMediator m, CancellationToken ct) =>
        {
            if (!_Helpers.TryUser(c, out var user)) return _Helpers.Unauthorized();
            c.Response.Headers.CacheControl = "private, no-store";
            try { return Results.Ok(ApiResponseHelper.Success(await m.Send(new ConfirmCommerceSessionCommand(user, id, body.Version), ct))); }
            catch (CommerceDomainException ex) { return Conflict(ex); }
        }).WithName("Commerce.ConfirmSession").WithTags("Commerce.Checkout").RequireAuthorization("UserOrAdmin");
        routes.MapDelete("/checkout/sessions/{id:guid}", async (Guid id, HttpContext c, IMediator m, CancellationToken ct) =>
        {
            if (!_Helpers.TryUser(c, out var user)) return _Helpers.Unauthorized();
            try { await m.Send(new ReleaseCommerceSessionCommand(user, id), ct); return Results.Ok(ApiResponseHelper.Success(true)); }
            catch (CommerceDomainException ex) { return Conflict(ex); }
        }).WithName("Commerce.ReleaseSession").WithTags("Commerce.Checkout").RequireAuthorization("UserOrAdmin");
    }
    private static IResult Conflict(CommerceDomainException ex) => Results.Conflict(ApiResponseHelper.Error(ex.Message, statusCode: 409));
    public sealed record StartBody(string ProviderKey, string IdempotencyKey, IReadOnlyList<CommerceSessionPassengerInput>? Passengers, string? CartVersion);
    public sealed record ConfirmBody(string Version);
}
