using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Refahi.Modules.SupplyChain.Application.Contracts.Commands.Agreements;
using Refahi.Shared.Presentation;

namespace Refahi.Modules.SupplyChain.Api.Endpoints.Agreements;

public class UpdateAgreementEndpoint : IEndpoint
{
    public void Map(object app)
    {
        if (app is not IEndpointRouteBuilder routes) return;

        routes.MapPut("/admin/agreements/{id:guid}", async (
            Guid id,
            [FromBody] UpdateAgreementRequest body,
            IMediator mediator,
            CancellationToken ct) =>
        {
            var command = new UpdateAgreementCommand(id, body.AgreementNo, body.Type, body.FromDate, body.ToDate);
            await mediator.Send(command, ct);
            return Results.Ok(ApiResponseHelper.Success<object>(null!, "قرارداد با موفقیت بروزرسانی شد"));
        })
        .WithName("SupplyChain.UpdateAgreement")
        .WithTags("SupplyChain.Agreements")
        .RequireAuthorization("AdminOnly")
        .Produces(StatusCodes.Status200OK)
        .Produces<ApiErrorResponse>(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden)
        .Produces(StatusCodes.Status404NotFound);
    }
}

public sealed record UpdateAgreementRequest(
    string AgreementNo,
    short Type,
    DateTimeOffset FromDate,
    DateTimeOffset ToDate);
