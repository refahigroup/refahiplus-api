using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Routing;
using Refahi.Modules.Charge.Application.Contracts.Features;
using Refahi.Modules.Charge.Application.Contracts.Providers;
using Refahi.Shared.Presentation;

namespace Refahi.Modules.Charge.Api.Endpoints;

public sealed class GetCatalogOffersEndpoint : IEndpoint
{
    public void Map(object app)
    {
        if (app is not IEndpointRouteBuilder routes)
            return;

        routes.MapPost("catalog/operators/{operator}/offers", async (string @operator, [FromBody] OffersBody body, ISender sender, CancellationToken ct)
            => Results.Ok(ApiResponseHelper.Success(await sender.Send(new GetOffersQuery(
                ChargeEndpointHelpers.ParseOperator(@operator),
                body.MobileNumber,
                body.Category), ct))))
            .RequireRateLimiting(ChargeRateLimiting.PublicCatalogPolicy)
            .WithName("Charge.Catalog.Offers")
            .WithTags("Charge.Catalog")
            .Produces<ApiResponse<IReadOnlyList<ChargeProductDto>>>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status429TooManyRequests);
    }
}
