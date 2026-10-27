using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Refahi.Modules.Entertainments.Api.Endpoints.Cart;
using Refahi.Modules.Entertainments.Application.Features.Checkout.Sessions;
using Refahi.Modules.Entertainments.Domain;
using Refahi.Shared.Presentation;

namespace Refahi.Modules.Entertainments.Api.Endpoints.Checkout;

public sealed class EntertainmentSessionsEndpoint : IEndpoint
{
    public void Map(object app)
    {
        if (app is not IEndpointRouteBuilder routes) return;
        routes.MapGet("/checkout/sessions/by-key", async (string key, HttpContext c, IMediator m, CancellationToken ct) =>
        {
            if (!_Helpers.TryUser(c, out var user)) return _Helpers.Unauthorized();
            c.Response.Headers.CacheControl = "private, no-store";
            var value = await m.Send(new FindEntertainmentSessionQuery(user, key), ct);
            return value == null ? Results.NotFound(ApiResponseHelper.Error("رزرو یافت نشد", statusCode: 404)) : Results.Ok(ApiResponseHelper.Success(value));
        }).WithName("Entertainment.FindSession").WithTags("Entertainment.Checkout").RequireAuthorization("UserOrAdmin");
        routes.MapPost("/checkout/sessions", async (StartBody body, HttpContext c, IMediator m, CancellationToken ct) =>
        {
            if (!_Helpers.TryUser(c, out var user)) return _Helpers.Unauthorized();
            c.Response.Headers.CacheControl = "private, no-store";
            try { return Results.Ok(ApiResponseHelper.Success(await m.Send(new StartEntertainmentSessionCommand(user, body.ProviderKey, body.IdempotencyKey, body.Passengers ?? [], body.CartVersion), ct))); }
            catch (EntertainmentDomainException ex) { return Conflict(ex); }
        }).WithName("Entertainment.StartSession").WithTags("Entertainment.Checkout").RequireAuthorization("UserOrAdmin");
        routes.MapGet("/checkout/sessions/{id:guid}", async (Guid id, HttpContext c, IMediator m, CancellationToken ct) =>
        {
            if (!_Helpers.TryUser(c, out var user)) return _Helpers.Unauthorized();
            c.Response.Headers.CacheControl = "private, no-store";
            try { return Results.Ok(ApiResponseHelper.Success(await m.Send(new GetEntertainmentSessionQuery(user, id), ct))); }
            catch (EntertainmentDomainException ex) { return Conflict(ex); }
        }).WithName("Entertainment.GetSession").WithTags("Entertainment.Checkout").RequireAuthorization("UserOrAdmin");
        routes.MapPost("/checkout/sessions/{id:guid}/confirm", async (Guid id, ConfirmBody body, HttpContext c, IMediator m, CancellationToken ct) =>
        {
            if (!_Helpers.TryUser(c, out var user)) return _Helpers.Unauthorized();
            c.Response.Headers.CacheControl = "private, no-store";
            try { return Results.Ok(ApiResponseHelper.Success(await m.Send(new ConfirmEntertainmentSessionCommand(user, id, body.Version), ct))); }
            catch (EntertainmentDomainException ex) { return Conflict(ex); }
        }).WithName("Entertainment.ConfirmSession").WithTags("Entertainment.Checkout").RequireAuthorization("UserOrAdmin");
        routes.MapDelete("/checkout/sessions/{id:guid}", async (Guid id, HttpContext c, IMediator m, CancellationToken ct) =>
        {
            if (!_Helpers.TryUser(c, out var user)) return _Helpers.Unauthorized();
            try { await m.Send(new ReleaseEntertainmentSessionCommand(user, id), ct); return Results.Ok(ApiResponseHelper.Success(true)); }
            catch (EntertainmentDomainException ex) { return Conflict(ex); }
        }).WithName("Entertainment.ReleaseSession").WithTags("Entertainment.Checkout").RequireAuthorization("UserOrAdmin");
    }
    private static IResult Conflict(EntertainmentDomainException ex) => Results.Conflict(ApiResponseHelper.Error(ex.Message, statusCode: 409));
    public sealed record StartBody(string ProviderKey, string IdempotencyKey, IReadOnlyList<EntertainmentSessionPassengerInput>? Passengers, string? CartVersion);
    public sealed record ConfirmBody(string Version);
}
