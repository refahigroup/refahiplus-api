using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Refahi.Modules.Store.Application.Contracts.Dtos.Banners;
using Refahi.Modules.Store.Application.Contracts.Queries.Banners;
using Refahi.Shared.Presentation;

namespace Refahi.Modules.Store.Api.Endpoints.Banners;

public class GetAdminBannersEndpoint : IEndpoint
{
    public void Map(object app)
    {
        if (app is not IEndpointRouteBuilder routes) return;

        routes.MapGet("/admin/banners", async (
            int? moduleId,
            short? bannerType,
            IMediator mediator,
            CancellationToken ct) =>
        {
            var result = await mediator.Send(new GetAdminBannersQuery(moduleId, bannerType), ct);
            return Results.Ok(ApiResponseHelper.Success(result));
        })
        .WithName("Store.GetAdminBanners")
        .WithTags("Store.Banners")
        .RequireAuthorization("AdminOnly")
        .Produces<ApiResponse<List<AdminBannerDto>>>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status401Unauthorized);
    }
}
