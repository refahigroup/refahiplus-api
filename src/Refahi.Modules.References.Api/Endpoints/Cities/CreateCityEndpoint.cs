using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Refahi.Modules.References.Application.Contracts.Commands;
using Refahi.Shared.Presentation;

namespace Refahi.Modules.References.Api.Endpoints.Cities;

public class CreateCityEndpoint : IEndpoint
{
    public void Map(object app)
    {
        if (app is not IEndpointRouteBuilder routes) return;

        routes.MapPost("/admin/cities", async (
            [FromBody] CreateCityCommand command,
            IMediator mediator,
            CancellationToken ct) =>
        {
            var result = await mediator.Send(command, ct);
            return Results.Created(
                $"/references/admin/cities/{result.Id}",
                ApiResponseHelper.Success(result, "شهر با موفقیت ایجاد شد", 201));
        })
        .WithName("References.CreateCity")
        .WithTags("References.Cities")
        .RequireAuthorization("AdminOnly")
        .Produces<ApiResponse<CreateCityResponse>>(StatusCodes.Status201Created)
        .Produces<ApiErrorResponse>(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status401Unauthorized);
    }
}
