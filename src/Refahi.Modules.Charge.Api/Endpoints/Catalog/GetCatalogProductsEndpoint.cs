using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Routing;
using Refahi.Modules.Charge.Application.Contracts.Features;
using Refahi.Modules.Charge.Application.Contracts.Providers;
using Refahi.Shared.Presentation;

namespace Refahi.Modules.Charge.Api.Endpoints;

public sealed class GetCatalogProductsEndpoint : IEndpoint
{
    public void Map(object app)
    {
        if (app is not IEndpointRouteBuilder routes)
            return;

        routes.MapGet("catalog/operators/{operator}/products", async (string @operator, ISender sender, CancellationToken ct)
            => Results.Ok(ApiResponseHelper.Success(await sender.Send(
                new GetProductsQuery(ChargeEndpointHelpers.ParseOperator(@operator)), ct))))
            .RequireRateLimiting(ChargeRateLimiting.PublicCatalogPolicy)
            .WithName("Charge.Catalog.Products")
            .WithTags("Charge.Catalog")
            .Produces<ApiResponse<IReadOnlyList<ChargeProductDto>>>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status429TooManyRequests);
    }
}
