using MediatR;
using System.Security.Claims;
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
        if (app is not IEndpointRouteBuilder routes)
            return;

        routes
            .MapPost(
                "/{orderId:guid}/cancel",
                async (
                    Guid orderId,
                    CancelOrderRequest request,
                    HttpContext httpContext,
                    IMediator mediator,
                    CancellationToken ct
                ) =>
                {
                    if (!Guid.TryParse(httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier)
                        ?? httpContext.User.FindFirstValue("sub"), out var userId))
                        return Results.Unauthorized();
                    var command = new CancelOrderCommand(
                        orderId,
                        request.Reason,
                        request.IdempotencyKey,
                        CallerUserId: userId,
                        CallerRole: httpContext.User.IsInRole("Admin") ? "Admin" : "User"
                    );
                    var result = await mediator.Send(command, ct);
                    return Results.Ok(ApiResponseHelper.Success(result));
                }
            )
            .WithName("Orders.CancelOrder")
            .WithTags("Orders")
            .RequireAuthorization("UserOrAdmin")
            .Produces<ApiResponse<CancelOrderResponse>>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status404NotFound);
    }
}
