using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Refahi.Modules.Store.Application.Contracts.Commands.Modules;
using Refahi.Shared.Presentation;

namespace Refahi.Modules.Store.Api.Endpoints.Modules;

public class UpdateModuleEndpoint : IEndpoint
{
    public void Map(object app)
    {
        if (app is not IEndpointRouteBuilder routes) return;

        routes.MapPut("/admin/modules/{id:int}", async (
            int id,
            [FromBody] UpdateModuleRequest body,
            IMediator mediator,
            CancellationToken ct) =>
        {
            var command = new UpdateModuleCommand(id, body.Name, body.Description, body.IconUrl, body.SortOrder);
            var result = await mediator.Send(command, ct);
            return Results.Ok(ApiResponseHelper.Success(result, "ماژول با موفقیت به‌روزرسانی شد"));
        })
        .WithName("Store.UpdateModule")
        .WithTags("Store.Modules")
        .RequireAuthorization("AdminOnly")
        .Produces<ApiResponse<UpdateModuleResponse>>(StatusCodes.Status200OK)
        .Produces<ApiErrorResponse>(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status404NotFound);
    }
}

public sealed record UpdateModuleRequest(
    string Name,
    string? Description,
    string? IconUrl,
    int SortOrder);
