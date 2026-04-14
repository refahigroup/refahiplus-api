using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Refahi.Modules.Store.Application.Contracts.Dtos.Banners;
using Refahi.Modules.Store.Application.Contracts.Queries.Banners;
using Refahi.Shared.Presentation;

namespace Refahi.Modules.Store.Api.Endpoints.Banners;

public class GetBannersEndpoint : IEndpoint
{
    public void Map(object app)
    {
        if (app is not IEndpointRouteBuilder routes) return;

        routes.MapGet("/banners", async (
            short? type,
            IMediator mediator,
            CancellationToken ct) =>
        {
            var result = await mediator.Send(new GetBannersQuery(type), ct);
            return Results.Ok(ApiResponseHelper.Success(result));
        })
        .WithName("Store.GetBanners")
        .WithTags("Store.Banners")
        .Produces<ApiResponse<List<BannerDto>>>(StatusCodes.Status200OK);
        // Public endpoint
    }
}
