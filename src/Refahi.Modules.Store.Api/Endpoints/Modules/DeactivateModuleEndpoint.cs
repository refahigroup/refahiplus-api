using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Refahi.Modules.Store.Application.Contracts.Commands.Modules;
using Refahi.Shared.Presentation;

namespace Refahi.Modules.Store.Api.Endpoints.Modules;

public class DeactivateModuleEndpoint : IEndpoint
{
    public void Map(object app)
    {
        if (app is not IEndpointRouteBuilder routes) return;

        routes.MapPost("/admin/modules/{id:int}/deactivate", async (
            int id,
            IMediator mediator,
            CancellationToken ct) =>
        {
            var result = await mediator.Send(new DeactivateModuleCommand(id), ct);
            return Results.Ok(ApiResponseHelper.Success(result, "ماژول با موفقیت غیرفعال شد"));
        })
        .WithName("Store.DeactivateModule")
        .WithTags("Store.Modules")
        .RequireAuthorization("AdminOnly")
        .Produces<ApiResponse<DeactivateModuleResponse>>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status404NotFound);
    }
}
