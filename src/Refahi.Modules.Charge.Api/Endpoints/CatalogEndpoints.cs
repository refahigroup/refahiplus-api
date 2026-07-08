using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Logging;
using Refahi.Modules.Charge.Application.Contracts.Features;
using Refahi.Modules.Charge.Application.Contracts.Providers;
using Refahi.Shared.Presentation;

namespace Refahi.Modules.Charge.Api.Endpoints;

public sealed class CatalogEndpoints : IEndpoint
{
    public void Map(object app)
    {
        if (app is not IEndpointRouteBuilder r) return;
        r.MapGet("catalog/operators", async (ISender s, CancellationToken ct)
            => Results.Ok(ApiResponseHelper.Success(await s.Send(new GetOperatorsQuery(), ct))))
            .RequireRateLimiting(ChargeRateLimiting.PublicCatalogPolicy)
            .WithName("Charge.Catalog.Operators").WithTags("Charge.Catalog")
            .Produces<ApiResponse<IReadOnlyList<ChargeOperatorDto>>>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status429TooManyRequests);
        r.MapGet("catalog/operators/{operator}/products", async (string @operator, ISender s, CancellationToken ct)
            => Results.Ok(ApiResponseHelper.Success(await s.Send(new GetProductsQuery(ChargeEndpointHelpers.ParseOperator(@operator)), ct))))
            .RequireRateLimiting(ChargeRateLimiting.PublicCatalogPolicy)
            .WithName("Charge.Catalog.Products").WithTags("Charge.Catalog")
            .Produces<ApiResponse<IReadOnlyList<ChargeProductDto>>>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status429TooManyRequests);
        r.MapPost("catalog/operators/{operator}/offers", async (string @operator, [FromBody] OffersBody body, ISender s, CancellationToken ct)
            => Results.Ok(ApiResponseHelper.Success(await s.Send(new GetOffersQuery(ChargeEndpointHelpers.ParseOperator(@operator), body.MobileNumber, body.Category), ct))))
            .RequireRateLimiting(ChargeRateLimiting.PublicCatalogPolicy)
            .WithName("Charge.Catalog.Offers").WithTags("Charge.Catalog")
            .Produces<ApiResponse<IReadOnlyList<ChargeProductDto>>>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status429TooManyRequests);
        r.MapPost("catalog/eligibility", async ([FromBody] EligibilityBody body, ISender s, CancellationToken ct)
            => Results.Ok(ApiResponseHelper.Success(await s.Send(new CheckEligibilityQuery(body.Operator, body.MobileNumber, body.AmountMinor, body.ProviderProductId, body.ProductCategory), ct))))
            .RequireAuthorization("UserOrAdmin").WithName("Charge.Catalog.Eligibility").WithTags("Charge.Catalog").Produces<ApiResponse<ChargeEligibilityDto>>(StatusCodes.Status200OK).Produces(StatusCodes.Status401Unauthorized);
        r.MapPost("catalog/operators/{operator}/postpaid-balance", async (string @operator, [FromBody] MobileBody body, ISender s, CancellationToken ct)
            => Results.Ok(ApiResponseHelper.Success(await s.Send(new GetPostpaidBalanceQuery(ChargeEndpointHelpers.ParseOperator(@operator), body.MobileNumber), ct))))
            .RequireRateLimiting(ChargeRateLimiting.PublicCatalogPolicy)
            .WithName("Charge.Catalog.PostpaidBalance").WithTags("Charge.Catalog")
            .Produces<ApiResponse<ChargePostpaidBalanceDto>>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status429TooManyRequests);
        r.MapGet("catalog/pin-categories", async ([FromQuery] Refahi.Modules.Charge.Domain.Enums.ChargeOperator? @operator, ISender s, CancellationToken ct)
            => Results.Ok(ApiResponseHelper.Success(await s.Send(new GetPinCategoriesQuery(@operator), ct))))
            .RequireRateLimiting(ChargeRateLimiting.PublicCatalogPolicy)
            .WithName("Charge.Catalog.PinCategories").WithTags("Charge.Catalog")
            .Produces<ApiResponse<IReadOnlyList<PinChargeCategoryDto>>>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status429TooManyRequests);
        r.MapGet("catalog/package-types", async (ISender s, CancellationToken ct)
            => Results.Ok(ApiResponseHelper.Success(await s.Send(new GetPackageTypesQuery(), ct))))
            .RequireAuthorization("UserOrAdmin").WithName("Charge.Catalog.PackageTypes").WithTags("Charge.Catalog").Produces<ApiResponse<IReadOnlyList<PackageTypeDto>>>(StatusCodes.Status200OK).Produces(StatusCodes.Status401Unauthorized);

        r.MapPost("catalog/quote", async (
            [FromBody] ChargeQuoteBody body,
            ILogger<CatalogEndpoints> logger,
            ISender sender,
            CancellationToken ct) =>
        {
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
public sealed record OffersBody(string MobileNumber, ChargeOfferCategory Category = ChargeOfferCategory.All);
public sealed record MobileBody(string MobileNumber);
public sealed record EligibilityBody(Refahi.Modules.Charge.Domain.Enums.ChargeOperator Operator, string MobileNumber, long AmountMinor, string ProviderProductId, int ProductCategory);
public sealed record ChargeQuoteBody(
    Refahi.Modules.Charge.Domain.Enums.ChargeOperator Operator,
    Refahi.Modules.Charge.Domain.Enums.ChargeServiceType ServiceType,
    string DestinationMobileNumber,
    string? ProviderProductId,
    long? RequestedAmountMinor,
    int? PinCategoryId,
    int PinCount = 1);
