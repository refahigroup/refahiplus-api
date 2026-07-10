using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Refahi.Modules.Charge.Application.Contracts.Features;
using Refahi.Modules.Charge.Application.Contracts.Providers;
using Refahi.Shared.Presentation;

namespace Refahi.Modules.Charge.Api.Endpoints;

public sealed class CheckCatalogEligibilityEndpoint : IEndpoint
{
    public void Map(object app)
    {
        if (app is not IEndpointRouteBuilder routes)
            return;

        routes.MapPost("catalog/eligibility", async ([FromBody] EligibilityBody body, ISender sender, CancellationToken ct)
            => Results.Ok(ApiResponseHelper.Success(await sender.Send(new CheckEligibilityQuery(
                body.Operator,
                body.MobileNumber,
                body.AmountMinor,
                body.ProviderProductId,
                body.ProductCategory), ct))))
            .RequireAuthorization("UserOrAdmin")
            .WithName("Charge.Catalog.Eligibility")
            .WithTags("Charge.Catalog")
            .Produces<ApiResponse<ChargeEligibilityDto>>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized);
    }
}
