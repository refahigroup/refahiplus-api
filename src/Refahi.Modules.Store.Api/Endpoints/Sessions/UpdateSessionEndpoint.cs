using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Refahi.Modules.Store.Application.Contracts.Commands.Sessions;
using Refahi.Shared.Presentation;

namespace Refahi.Modules.Store.Api.Endpoints.Sessions;

public class UpdateSessionEndpoint : IEndpoint
{
    public void Map(object app)
    {
        if (app is not IEndpointRouteBuilder routes) return;

        routes.MapPut("/provider/sessions/{id:guid}", async (
            Guid id,
            [FromBody] UpdateSessionCommand command,
            IMediator mediator,
            CancellationToken ct) =>
        {
            var adjustedCommand = command with { SessionId = id };
            var result = await mediator.Send(adjustedCommand, ct);
            return Results.Ok(ApiResponseHelper.Success(result, "سانس با موفقیت به‌روزرسانی شد"));
        })
        .WithName("Store.UpdateSession")
        .WithTags("Store.Sessions")
        .RequireAuthorization("ProviderOrAdmin")
        .Produces<ApiResponse<UpdateSessionResponse>>(StatusCodes.Status200OK)
        .Produces<ApiErrorResponse>(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status404NotFound);
    }
}
