using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Routing;
using Refahi.Modules.Charge.Application.Contracts.Features;
using Refahi.Shared.Presentation;

namespace Refahi.Modules.Charge.Api.Endpoints;

public sealed class GetCatalogOperatorsEndpoint : IEndpoint
{
    public void Map(object app)
    {
        if (app is not IEndpointRouteBuilder routes)
            return;

        routes.MapGet("catalog/operators", async (ISender sender, CancellationToken ct)
            => Results.Ok(ApiResponseHelper.Success(await sender.Send(new GetOperatorsQuery(), ct))))
            .RequireRateLimiting(ChargeRateLimiting.PublicCatalogPolicy)
            .WithName("Charge.Catalog.Operators")
            .WithTags("Charge.Catalog")
            .Produces<ApiResponse<IReadOnlyList<ChargeOperatorDto>>>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status429TooManyRequests);
    }
}
