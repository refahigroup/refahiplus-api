using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Refahi.Modules.Orders.Application.Contracts.Queries;
using Refahi.Shared.Presentation;

namespace Refahi.Modules.Orders.Api.Endpoints;

public class GetOrdersBySourceEndpoint : IEndpoint
{
    public void Map(object app)
    {
        if (app is not IEndpointRouteBuilder routes) return;

        routes.MapGet("/by-source", async (
            IMediator mediator,
            string module,
            Guid referenceId,
            int pageNumber = 1,
            int pageSize = 20,
            CancellationToken ct = default) =>
        {
            var result = await mediator.Send(
                new GetOrdersBySourceQuery(module, referenceId, pageNumber, pageSize), ct);
            return Results.Ok(ApiResponseHelper.SuccessPaginated(result.Data, result.PageNumber, result.PageSize, result.TotalCount));
        })
        .WithName("Orders.GetOrdersBySource")
        .WithTags("Orders")
        .RequireAuthorization("ProviderOrAdmin")
        .Produces<PaginatedResponse<object>>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden);
    }
}
