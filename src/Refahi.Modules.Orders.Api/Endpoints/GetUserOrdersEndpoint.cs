using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Refahi.Modules.Orders.Application.Contracts.Queries;
using Refahi.Shared.Presentation;
using System.Security.Claims;

namespace Refahi.Modules.Orders.Api.Endpoints;

public class GetUserOrdersEndpoint : IEndpoint
{
    public void Map(object app)
    {
        if (app is not IEndpointRouteBuilder routes) return;

        routes.MapGet("/my", async (
            HttpContext httpContext,
            IMediator mediator,
            int pageNumber = 1,
            int pageSize = 20,
            CancellationToken ct = default) =>
        {
            var userIdClaim = httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier)
                ?? httpContext.User.FindFirstValue("sub");

            if (userIdClaim is null || !Guid.TryParse(userIdClaim, out var userId))
                return Results.Unauthorized();

            var result = await mediator.Send(new GetUserOrdersQuery(userId, pageNumber, pageSize), ct);
            return Results.Ok(ApiResponseHelper.SuccessPaginated(result.Data, result.PageNumber, result.PageSize, result.TotalCount));
        })
        .WithName("Orders.GetMyOrders")
        .WithTags("Orders")
        .RequireAuthorization("UserOrAdmin")
        .Produces<PaginatedResponse<object>>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status401Unauthorized);
    }
}
