using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Refahi.Modules.Store.Application.Contracts.Dtos.Products;
using Refahi.Modules.Store.Application.Contracts.Queries.Sessions;
using Refahi.Shared.Presentation;

namespace Refahi.Modules.Store.Api.Endpoints.Sessions;

public class GetProductSessionsEndpoint : IEndpoint
{
    public void Map(object app)
    {
        if (app is not IEndpointRouteBuilder routes) return;

        routes.MapGet("/products/{productId:guid}/sessions", async (
            Guid productId,
            IMediator mediator,
            CancellationToken ct) =>
        {
            var result = await mediator.Send(new GetProductSessionsQuery(productId), ct);
            return Results.Ok(ApiResponseHelper.Success(result));
        })
        .WithName("Store.GetProductSessions")
        .WithTags("Store.Sessions")
        .Produces<ApiResponse<List<ProductSessionDto>>>(StatusCodes.Status200OK);
        // Public endpoint
    }
}
