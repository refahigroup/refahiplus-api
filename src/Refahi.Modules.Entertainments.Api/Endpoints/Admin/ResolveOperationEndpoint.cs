using Refahi.Modules.Entertainments.Application.Features.Operations;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Refahi.Modules.Entertainments.Api.Endpoints.Catalog;
using Refahi.Modules.Entertainments.Api.Endpoints.Cart;
using Refahi.Modules.Entertainments.Application;
using Refahi.Modules.Entertainments.Application.Contracts;
using Refahi.Modules.Entertainments.Application.Features.Operations.ResolveOperation;
using Refahi.Shared.Presentation;

namespace Refahi.Modules.Entertainments.Api.Endpoints.Admin;

public sealed class ResolveOperationEndpoint : IEndpoint
{
    public void Map(object app)
    {
        if (app is not IEndpointRouteBuilder routes)
            return;

        routes.MapGet("/admin/reservations", async (IMediator sender, CancellationToken ct) =>
            Results.Ok(ApiResponseHelper.Success(await sender.Send(new GetUncertainEntertainmentReservationsQuery(), ct))))
            .RequireAuthorization("AdminOnly").WithName("Entertainments.UncertainReservations").WithTags("Entertainments.Admin");
        routes.MapPost("/admin/reservations/{id:guid}/resolve", async (Guid id, ReservationResolutionBody body, HttpContext context, IMediator sender, CancellationToken ct) => {
            if (!_Helpers.TryUser(context, out var actor)) return _Helpers.Unauthorized();
            await sender.Send(new ResolveEntertainmentReservationCommand(id, actor, body.Outcome, body.Evidence, body.IdempotencyKey), ct);
            return Results.Ok(ApiResponseHelper.Success(true));
        }).RequireAuthorization("AdminOnly").WithName("Entertainments.ResolveReservation").WithTags("Entertainments.Admin");
        routes.MapPost("/admin/operations/{fulfillmentId:guid}/recheck", async (Guid fulfillmentId, IMediator m, CancellationToken ct) =>
        {
            await m.Send(new RecheckEntertainmentOperationCommand(fulfillmentId), ct);
            return Results.Ok(ApiResponseHelper.Success(true));
        }).WithName("Entertainment.Admin.RecheckOperation").WithTags("Entertainment.Admin").RequireAuthorization("AdminOnly");

        routes.MapPost(
            "/admin/operations/{fulfillmentId:guid}/resolve",
            async (
                Guid fulfillmentId,
                ResolveOperationBody body,
                HttpContext context,
                IMediator m,
                CancellationToken ct
            ) =>
            {
                if (!Refahi.Modules.Entertainments.Api.Endpoints.Cart._Helpers.TryUser(context, out var actor)) return Refahi.Modules.Entertainments.Api.Endpoints.Cart._Helpers.Unauthorized();
                await m.Send(new ResolveEntertainmentOperationCommand(
                    fulfillmentId,
                    body.Outcome,
                    body.Evidence,
                    body.ProviderOrderCode,
                    body.Tickets, actor, body.IdempotencyKey
                ), ct);

                return Results.Ok(ApiResponseHelper.Success(true));
            }
        )
        .WithName("Entertainment.Admin.ResolveOperation")
        .WithTags("Entertainment.Admin")
        .RequireAuthorization("AdminOnly");
    }

    public sealed record ReservationResolutionBody(string Outcome, string Evidence, string IdempotencyKey);
    public sealed record ResolveOperationBody(string Outcome, string Evidence, string? ProviderOrderCode, IReadOnlyList<ResolvedTicketInput>? Tickets, string IdempotencyKey);
}

