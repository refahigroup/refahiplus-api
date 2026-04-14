using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Refahi.Modules.Store.Application.Contracts.Commands.Sessions;
using Refahi.Shared.Presentation;

namespace Refahi.Modules.Store.Api.Endpoints.Sessions;

public class CancelSessionEndpoint : IEndpoint
{
    public void Map(object app)
    {
        if (app is not IEndpointRouteBuilder routes) return;

        routes.MapDelete("/provider/sessions/{id:guid}", async (
            Guid id,
            IMediator mediator,
            CancellationToken ct) =>
        {
            var result = await mediator.Send(new CancelSessionCommand(id), ct);
            return Results.Ok(ApiResponseHelper.Success(result, "سانس با موفقیت لغو شد"));
        })
        .WithName("Store.CancelSession")
        .WithTags("Store.Sessions")
        .RequireAuthorization("ProviderOrAdmin")
        .Produces<ApiResponse<CancelSessionResponse>>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status404NotFound);
    }
}
