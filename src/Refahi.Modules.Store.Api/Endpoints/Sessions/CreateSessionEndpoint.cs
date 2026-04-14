using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Refahi.Modules.Store.Application.Contracts.Commands.Sessions;
using Refahi.Shared.Presentation;

namespace Refahi.Modules.Store.Api.Endpoints.Sessions;

public class CreateSessionEndpoint : IEndpoint
{
    public void Map(object app)
    {
        if (app is not IEndpointRouteBuilder routes) return;

        routes.MapPost("/provider/products/{productId:guid}/sessions", async (
            Guid productId,
            [FromBody] CreateSessionCommand command,
            IMediator mediator,
            CancellationToken ct) =>
        {
            var adjustedCommand = command with { ProductId = productId };
            var result = await mediator.Send(adjustedCommand, ct);
            return Results.Created(
                $"/products/{productId}/sessions",
                ApiResponseHelper.Success(result, "سانس با موفقیت ایجاد شد", 201));
        })
        .WithName("Store.CreateSession")
        .WithTags("Store.Sessions")
        .RequireAuthorization("ProviderOrAdmin")
        .Produces<ApiResponse<CreateSessionResponse>>(StatusCodes.Status201Created)
        .Produces<ApiErrorResponse>(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status404NotFound);
    }
}
