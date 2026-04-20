using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Refahi.Modules.Store.Application.Contracts.Dtos.Banners;
using Refahi.Modules.Store.Application.Contracts.Queries.Banners;
using Refahi.Modules.Store.Application.Services;
using Refahi.Shared.Presentation;

namespace Refahi.Modules.Store.Api.Endpoints.Banners;

public class GetBannersEndpoint : IEndpoint
{
    public void Map(object app)
    {
        if (app is not IEndpointRouteBuilder routes) return;

        routes.MapGet("/{moduleSlug}/banners", async (
            string moduleSlug,
            short? type,
            IModuleResolver moduleResolver,
            IMediator mediator,
            CancellationToken ct) =>
        {
            var moduleId = await moduleResolver.ResolveIdAsync(moduleSlug, ct);
            if (moduleId is null)
                return Results.NotFound();

            var result = await mediator.Send(new GetBannersQuery(moduleId, type), ct);
            return Results.Ok(ApiResponseHelper.Success(result));
        })
        .WithName("Store.GetBanners")
        .WithTags("Store.Banners")
        .Produces<ApiResponse<List<BannerDto>>>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status404NotFound);
        // Public endpoint
    }
}
