using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Refahi.Modules.Store.Application.Contracts.Dtos.Shops;
using Refahi.Modules.Store.Application.Contracts.Queries.Shops;
using Refahi.Shared.Presentation;

namespace Refahi.Modules.Store.Api.Endpoints.Shops;

public class GetShopsEndpoint : IEndpoint
{
    public void Map(object app)
    {
        if (app is not IEndpointRouteBuilder routes) return;

        routes.MapGet("/shops", async (
            short? type,
            int page,
            int size,
            IMediator mediator,
            CancellationToken ct) =>
        {
            var query = new GetShopsQuery(
                ShopType: type,
                Status: null,       // defaults to Active in handler
                PageNumber: page > 0 ? page : 1,
                PageSize: size > 0 ? size : 20);

            var result = await mediator.Send(query, ct);
            return Results.Ok(ApiResponseHelper.SuccessPaginated(
                result.Data,
                result.PageNumber,
                result.PageSize,
                result.TotalCount));
        })
        .WithName("Store.GetShops")
        .WithTags("Store.Shops")
        .Produces<PaginatedResponse<ShopSummaryDto>>(StatusCodes.Status200OK);
        // No RequireAuthorization — public endpoint
    }
}
