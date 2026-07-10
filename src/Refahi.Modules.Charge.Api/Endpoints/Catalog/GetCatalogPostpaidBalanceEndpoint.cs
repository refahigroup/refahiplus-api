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

public sealed class GetCatalogPostpaidBalanceEndpoint : IEndpoint
{
    public void Map(object app)
    {
        if (app is not IEndpointRouteBuilder routes)
            return;

        routes.MapPost("catalog/operators/{operator}/postpaid-balance", async (string @operator, [FromBody] MobileBody body, ISender sender, CancellationToken ct)
            => Results.Ok(ApiResponseHelper.Success(await sender.Send(new GetPostpaidBalanceQuery(
                ChargeEndpointHelpers.ParseOperator(@operator),
                body.MobileNumber), ct))))
            .RequireRateLimiting(ChargeRateLimiting.PublicCatalogPolicy)
            .WithName("Charge.Catalog.PostpaidBalance")
            .WithTags("Charge.Catalog")
            .Produces<ApiResponse<ChargePostpaidBalanceDto>>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status429TooManyRequests);
    }
}
