using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Refahi.Modules.Commerce.Api.Endpoints.Catalog;
using Refahi.Modules.Commerce.Application;
using Refahi.Modules.Commerce.Application.Contracts;
using Refahi.Modules.Commerce.Application.Features.Operations.ResolveOperation;
using Refahi.Shared.Presentation;

namespace Refahi.Modules.Commerce.Api.Endpoints.Admin;

public sealed class ResolveOperationEndpoint : IEndpoint
{
    public void Map(object app)
    {
        if (app is not IEndpointRouteBuilder routes)
            return;

        routes.MapPost("/admin/fulfillments/{fulfillmentId:guid}/recheck", async (Guid fulfillmentId, IMediator m, CancellationToken ct) =>
        {
            await m.Send(new RecheckCommerceOperationCommand(fulfillmentId), ct);
            return Results.Ok(ApiResponseHelper.Success(true));
        }).WithName("Commerce.Admin.RecheckOperation").WithTags("Commerce.Admin").RequireAuthorization("AdminOnly");

        routes.MapPost(
            "/admin/fulfillments/{fulfillmentId:guid}/resolve",
            async (
                Guid fulfillmentId,
                ResolveOperationBody body,
                IMediator m,
                CancellationToken ct
            ) =>
            {
                await m.Send(new ResolveCommerceOperationCommand(
                    fulfillmentId,
                    body.Outcome,
                    body.Evidence,
                    body.ProviderOrderCode,
                    body.Tickets
                ), ct);

                return Results.Ok(ApiResponseHelper.Success(true));
            }
        )
        .WithName("Commerce.Admin.ResolveOperation")
        .WithTags("Commerce.Admin")
        .RequireAuthorization("AdminOnly");
    }

    public sealed record ResolveOperationBody(string Outcome, string Evidence, string? ProviderOrderCode, IReadOnlyList<ResolvedTicketInput>? Tickets);
}

