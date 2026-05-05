using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Refahi.Modules.Store.Application.Contracts.Dtos.DailyDeals;
using Refahi.Modules.Store.Application.Contracts.Queries.DailyDeals;
using Refahi.Modules.Store.Application.Services;
using Refahi.Modules.Store.Domain.Enums;
using Refahi.Modules.Store.Domain.Repositories;
using Refahi.Shared.Presentation;

namespace Refahi.Modules.Store.Api.Endpoints.DailyDeals;

public class GetDailyDealsEndpoint : IEndpoint
{
    public void Map(object app)
    {
        if (app is not IEndpointRouteBuilder routes) return;

        routes.MapGet("/daily-deals", async (
            string ownerType,
            string ownerSlug,
            IModuleResolver moduleResolver,
            IShopRepository shopRepo,
            IMediator mediator,
            CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(ownerType) || string.IsNullOrWhiteSpace(ownerSlug))
                return Results.BadRequest(ApiResponseHelper.Error("نوع مالک و شناسه‌ی آن الزامی است"));

            BannerOwnerType parsedType;
            string ownerId;

            if (string.Equals(ownerType, "module", StringComparison.OrdinalIgnoreCase))
            {
                parsedType = BannerOwnerType.Module;
                var moduleId = await moduleResolver.ResolveIdAsync(ownerSlug, ct);
                if (moduleId is null)
                    return Results.NotFound();
                ownerId = moduleId.Value.ToString();
            }
            else if (string.Equals(ownerType, "shop", StringComparison.OrdinalIgnoreCase))
            {
                parsedType = BannerOwnerType.Shop;
                var shop = await shopRepo.GetBySlugAsync(ownerSlug, ct);
                if (shop is null)
                    return Results.NotFound();
                ownerId = shop.Id.ToString();
            }
            else
            {
                return Results.BadRequest(ApiResponseHelper.Error("نوع مالک نامعتبر است"));
            }

            var result = await mediator.Send(new GetDailyDealsQuery(parsedType, ownerId), ct);
            return Results.Ok(ApiResponseHelper.Success(result));
        })
        .WithName("Store.GetDailyDeals")
        .WithTags("Store.DailyDeals")
        .Produces<ApiResponse<List<DailyDealDto>>>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status404NotFound)
        .Produces(StatusCodes.Status400BadRequest);
        // Public endpoint
    }
}
