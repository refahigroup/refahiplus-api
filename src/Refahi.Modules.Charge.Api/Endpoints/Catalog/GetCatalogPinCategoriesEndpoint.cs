using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Routing;
using Refahi.Modules.Charge.Application.Contracts.Features;
using Refahi.Modules.Charge.Application.Contracts.Providers;
using Refahi.Modules.Charge.Domain.Enums;
using Refahi.Shared.Presentation;

namespace Refahi.Modules.Charge.Api.Endpoints;

public sealed class GetCatalogPinCategoriesEndpoint : IEndpoint
{
    public void Map(object app)
    {
        if (app is not IEndpointRouteBuilder routes)
            return;

        routes.MapGet("catalog/pin-categories", async ([FromQuery] ChargeOperator? @operator, ISender sender, CancellationToken ct)
            => Results.Ok(ApiResponseHelper.Success(await sender.Send(new GetPinCategoriesQuery(@operator), ct))))
            .RequireRateLimiting(ChargeRateLimiting.PublicCatalogPolicy)
            .WithName("Charge.Catalog.PinCategories")
            .WithTags("Charge.Catalog")
            .Produces<ApiResponse<IReadOnlyList<PinChargeCategoryDto>>>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status429TooManyRequests);
    }
}
