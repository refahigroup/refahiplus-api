using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Refahi.Modules.Store.Application.Contracts.Dtos.Banners;
using Refahi.Modules.Store.Application.Contracts.Queries.Banners;
using Refahi.Modules.Store.Application.Services;
using Refahi.Modules.Store.Domain.Enums;
using Refahi.Modules.Store.Domain.Repositories;
using Refahi.Shared.Presentation;

namespace Refahi.Modules.Store.Api.Endpoints.Banners;

public class GetBannersEndpoint : IEndpoint
{
    public void Map(object app)
    {
        if (app is not IEndpointRouteBuilder routes) return;

        routes.MapGet("/banners", async (
            string ownerType,
            string ownerSlug,
            short? type,
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

            var result = await mediator.Send(new GetBannersQuery(parsedType, ownerId, type), ct);
            return Results.Ok(ApiResponseHelper.Success(result));
        })
        .WithName("Store.GetBanners")
        .WithTags("Store.Banners")
        .Produces<ApiResponse<List<BannerDto>>>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status404NotFound)
        .Produces(StatusCodes.Status400BadRequest);
        // Public endpoint
    }
}
