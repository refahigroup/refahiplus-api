using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Refahi.Modules.Orders.Application.Contracts.Queries;
using Refahi.Shared.Presentation;

namespace Refahi.Modules.Orders.Api.Endpoints;

public class GetAllOrdersEndpoint : IEndpoint
{
    public void Map(object app)
    {
        if (app is not IEndpointRouteBuilder routes) return;

        routes.MapGet("/admin", async (
            IMediator mediator,
            int pageNumber = 1,
            int pageSize = 20,
            string? status = null,
            Guid? userId = null,
            string? sourceModule = null,
            CancellationToken ct = default) =>
        {
            var result = await mediator.Send(
                new GetAllOrdersQuery(pageNumber, pageSize, status, userId, sourceModule), ct);
            return Results.Ok(ApiResponseHelper.SuccessPaginated(result.Data, result.PageNumber, result.PageSize, result.TotalCount));
        })
        .WithName("Orders.GetAllOrders")
        .WithTags("Orders")
        .RequireAuthorization("AdminOnly")
        .Produces<PaginatedResponse<object>>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden);
    }
}
