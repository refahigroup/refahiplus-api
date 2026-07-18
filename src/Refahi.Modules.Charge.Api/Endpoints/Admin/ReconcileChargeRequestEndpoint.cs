using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Refahi.Modules.Charge.Application.Contracts.Features;
using Refahi.Shared.Presentation;

namespace Refahi.Modules.Charge.Api.Endpoints;

public sealed class ReconcileChargeRequestEndpoint : IEndpoint
{
    public void Map(object app)
    {
        if (app is not IEndpointRouteBuilder routes)
            return;

        routes.MapPost("admin/charge-requests/{id:guid}/reconcile", async (Guid id, [FromQuery] bool force, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(new ReconcileChargeRequestCommand(id, force), ct);
            return Results.Ok(ApiResponseHelper.Success(result, "بازبینی تراکنش انجام شد"));
        })
        .RequireAuthorization("AdminOnly")
        .WithName("Charge.Admin.Reconcile")
        .WithTags("Charge.Admin")
        .Produces<ApiResponse<ReconcileChargeRequestResponse>>(StatusCodes.Status200OK);
    }
}
