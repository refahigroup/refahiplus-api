using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;
using Refahi.Modules.Charge.Application.Contracts.Features;
using Refahi.Shared.Presentation;

namespace Refahi.Modules.Charge.Api.Endpoints;

public sealed class PreviewChargeQuoteEndpoint : IEndpoint
{
    public void Map(object app)
    {
        if (app is not IEndpointRouteBuilder routes)
            return;

        routes.MapPost("catalog/quote", async (
            [FromBody] ChargeQuoteBody body,
            ILoggerFactory loggerFactory,
            ISender sender,
            CancellationToken ct) =>
        {
            var logger = loggerFactory.CreateLogger("Refahi.Modules.Charge.Api.Endpoints.CatalogEndpoints");
            using var scope = logger.BeginScope(new Dictionary<string, object?>
            {
                ["DestinationMobile"] = Mask(body.DestinationMobileNumber),
                ["Operator"] = body.Operator,
                ["ServiceType"] = body.ServiceType
            });

            var quote = await sender.Send(new PreviewChargeRequestCommand(
                body.Operator,
                body.ServiceType,
                body.DestinationMobileNumber,
                body.ProviderProductId,
                body.RequestedAmountMinor,
                body.PinCategoryId,
                body.PinCount), ct);

            return Results.Ok(ApiResponseHelper.Success(quote, "پیش‌نمایش خرید شارژ آماده شد"));
        })
        .RequireRateLimiting(ChargeRateLimiting.PublicCatalogPolicy)
        .WithName("Charge.Catalog.Quote")
        .WithTags("Charge.Catalog")
        .Produces<ApiResponse<ChargeRequestQuoteResponse>>(StatusCodes.Status200OK)
        .Produces<ApiErrorResponse>(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status429TooManyRequests);
    }

    private static string Mask(string value)
        => value.Length < 8 ? "***" : $"{value[..4]}***{value[^4..]}";
}
