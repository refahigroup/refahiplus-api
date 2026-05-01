using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Refahi.Modules.SupplyChain.Application.Contracts.Commands.Agreements;
using Refahi.Shared.Presentation;

namespace Refahi.Modules.SupplyChain.Api.Endpoints.Agreements;

public class ChangeAgreementStatusEndpoint : IEndpoint
{
    public void Map(object app)
    {
        if (app is not IEndpointRouteBuilder routes) return;

        routes.MapPatch("/admin/agreements/{id:guid}/status", async (
            Guid id,
            [FromBody] ChangeAgreementStatusRequest body,
            IMediator mediator,
            CancellationToken ct) =>
        {
            var command = new ChangeAgreementStatusCommand(id, body.NewStatus, body.Note);
            await mediator.Send(command, ct);
            return Results.Ok(ApiResponseHelper.Success<object>(null!, "وضعیت قرارداد با موفقیت تغییر یافت"));
        })
        .WithName("SupplyChain.ChangeAgreementStatus")
        .WithTags("SupplyChain.Agreements")
        .RequireAuthorization("AdminOnly")
        .Produces(StatusCodes.Status200OK)
        .Produces<ApiErrorResponse>(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden)
        .Produces(StatusCodes.Status404NotFound);
    }
}

public sealed record ChangeAgreementStatusRequest(short NewStatus, string? Note);
