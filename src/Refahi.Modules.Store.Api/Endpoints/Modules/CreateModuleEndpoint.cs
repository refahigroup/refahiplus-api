using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Refahi.Modules.Store.Application.Contracts.Commands.Modules;
using Refahi.Shared.Presentation;

namespace Refahi.Modules.Store.Api.Endpoints.Modules;

public class CreateModuleEndpoint : IEndpoint
{
    public void Map(object app)
    {
        if (app is not IEndpointRouteBuilder routes) return;

        routes.MapPost("/admin/modules", async (
            [FromBody] CreateModuleCommand command,
            IMediator mediator,
            CancellationToken ct) =>
        {
            var result = await mediator.Send(command, ct);
            return Results.Created(
                $"/modules/{result.Id}",
                ApiResponseHelper.Success(result, "ماژول با موفقیت ایجاد شد", 201));
        })
        .WithName("Store.CreateModule")
        .WithTags("Store.Modules")
        .RequireAuthorization("AdminOnly")
        .Produces<ApiResponse<CreateModuleResponse>>(StatusCodes.Status201Created)
        .Produces<ApiErrorResponse>(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status401Unauthorized);
    }
}
