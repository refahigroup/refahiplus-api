using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Refahi.Modules.Orders.Application.Contracts.Commands;
using Refahi.Shared.Presentation;

namespace Refahi.Modules.Orders.Api.Endpoints;

public record CancelOrderRequest(string? Reason, string IdempotencyKey);

public class CancelOrderEndpoint : IEndpoint
{
    public void Map(object app)
    {
        if (app is not IEndpointRouteBuilder routes) return;

        routes.MapPost("/{orderId:guid}/cancel", async (
            Guid orderId,
            CancelOrderRequest request,
            IMediator mediator,
            CancellationToken ct) =>
        {
            var command = new CancelOrderCommand(orderId, request.Reason, request.IdempotencyKey);
            var result = await mediator.Send(command, ct);
            return Results.Ok(ApiResponseHelper.Success(result));
        })
        .WithName("Orders.CancelOrder")
        .WithTags("Orders")
        .RequireAuthorization("UserOrAdmin")
        .Produces<ApiResponse<CancelOrderResponse>>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status404NotFound);
    }
}
