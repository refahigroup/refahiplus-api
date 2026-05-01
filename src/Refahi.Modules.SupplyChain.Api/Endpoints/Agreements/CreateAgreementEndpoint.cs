using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Refahi.Modules.SupplyChain.Application.Contracts.Commands.Agreements;
using Refahi.Shared.Presentation;

namespace Refahi.Modules.SupplyChain.Api.Endpoints.Agreements;

public class CreateAgreementEndpoint : IEndpoint
{
    public void Map(object app)
    {
        if (app is not IEndpointRouteBuilder routes) return;

        routes.MapPost("/admin/agreements", async (
            [FromBody] CreateAgreementCommand command,
            IMediator mediator,
            CancellationToken ct) =>
        {
            var result = await mediator.Send(command, ct);
            return Results.Created(
                $"/api/supply-chain/admin/agreements/{result.Id}",
                ApiResponseHelper.Success(result, "قرارداد با موفقیت ایجاد شد", 201));
        })
        .WithName("SupplyChain.CreateAgreement")
        .WithTags("SupplyChain.Agreements")
        .RequireAuthorization("AdminOnly")
        .Produces<ApiResponse<CreateAgreementResponse>>(StatusCodes.Status201Created)
        .Produces<ApiErrorResponse>(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden);
    }
}
