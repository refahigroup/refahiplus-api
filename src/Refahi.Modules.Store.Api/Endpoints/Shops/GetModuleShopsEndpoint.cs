using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Refahi.Modules.Store.Application.Contracts.Dtos.Shops;
using Refahi.Modules.Store.Application.Contracts.Queries.Shops;
using Refahi.Modules.Store.Application.Services;
using Refahi.Shared.Presentation;

namespace Refahi.Modules.Store.Api.Endpoints.Shops;

public class GetModuleShopsEndpoint : IEndpoint
{
    public void Map(object app)
    {
        if (app is not IEndpointRouteBuilder routes) return;

        routes.MapGet("/{moduleSlug}/shops", async (
            string moduleSlug,
            int page,
            int size,
            IModuleResolver moduleResolver,
            IMediator mediator,
            CancellationToken ct) =>
        {
            var moduleId = await moduleResolver.ResolveIdAsync(moduleSlug, ct);
            if (moduleId is null)
                return Results.NotFound();

            var query = new GetModuleShopsQuery(
                ModuleId: moduleId.Value,
                PageNumber: page > 0 ? page : 1,
                PageSize: size > 0 ? size : 20);

            var result = await mediator.Send(query, ct);
            return Results.Ok(ApiResponseHelper.SuccessPaginated(
                result.Data,
                result.PageNumber,
                result.PageSize,
                result.TotalCount));
        })
        .WithName("Store.GetModuleShops")
        .WithTags("Store.Shops")
        .Produces<PaginatedResponse<ShopSummaryDto>>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status404NotFound);
    }
}
