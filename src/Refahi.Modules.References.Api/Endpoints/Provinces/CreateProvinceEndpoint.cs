using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Refahi.Modules.References.Application.Contracts.Commands;
using Refahi.Shared.Presentation;

namespace Refahi.Modules.References.Api.Endpoints.Provinces;

public class CreateProvinceEndpoint : IEndpoint
{
    public void Map(object app)
    {
        if (app is not IEndpointRouteBuilder routes) return;

        routes.MapPost("/admin/provinces", async (
            [FromBody] CreateProvinceCommand command,
            IMediator mediator,
            CancellationToken ct) =>
        {
            var result = await mediator.Send(command, ct);
            return Results.Created(
                $"/references/admin/provinces/{result.Id}",
                ApiResponseHelper.Success(result, "استان با موفقیت ایجاد شد", 201));
        })
        .WithName("References.CreateProvince")
        .WithTags("References.Provinces")
        .RequireAuthorization("AdminOnly")
        .Produces<ApiResponse<CreateProvinceResponse>>(StatusCodes.Status201Created)
        .Produces<ApiErrorResponse>(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status401Unauthorized);
    }
}
