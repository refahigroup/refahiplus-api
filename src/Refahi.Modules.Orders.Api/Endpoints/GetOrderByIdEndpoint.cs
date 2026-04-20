using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Refahi.Modules.Orders.Application.Contracts.Dtos;
using Refahi.Modules.Orders.Application.Contracts.Queries;
using Refahi.Shared.Presentation;
using System.Security.Claims;

namespace Refahi.Modules.Orders.Api.Endpoints;

public class GetOrderByIdEndpoint : IEndpoint
{
    public void Map(object app)
    {
        if (app is not IEndpointRouteBuilder routes) return;

        routes.MapGet("/{orderId:guid}", async (
            Guid orderId,
            HttpContext httpContext,
            IMediator mediator,
            CancellationToken ct) =>
        {
            var userIdClaim = httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier)
                ?? httpContext.User.FindFirstValue("sub");

            if (userIdClaim is null || !Guid.TryParse(userIdClaim, out var callerId))
                return Results.Unauthorized();

            var callerRole = httpContext.User.FindFirstValue(ClaimTypes.Role) ?? "User";

            var result = await mediator.Send(new GetOrderByIdQuery(orderId, callerId, callerRole), ct);
            return result is null
                ? Results.NotFound()
                : Results.Ok(ApiResponseHelper.Success(result));
        })
        .WithName("Orders.GetOrderById")
        .WithTags("Orders")
        .RequireAuthorization("UserOrAdmin")
        .Produces<ApiResponse<OrderDto>>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status404NotFound);
    }
}
