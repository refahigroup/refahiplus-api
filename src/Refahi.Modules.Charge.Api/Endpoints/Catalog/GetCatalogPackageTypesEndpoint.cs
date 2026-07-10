using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Refahi.Modules.Charge.Application.Contracts.Features;
using Refahi.Modules.Charge.Application.Contracts.Providers;
using Refahi.Shared.Presentation;

namespace Refahi.Modules.Charge.Api.Endpoints;

public sealed class GetCatalogPackageTypesEndpoint : IEndpoint
{
    public void Map(object app)
    {
        if (app is not IEndpointRouteBuilder routes)
            return;

        routes.MapGet("catalog/package-types", async (ISender sender, CancellationToken ct)
            => Results.Ok(ApiResponseHelper.Success(await sender.Send(new GetPackageTypesQuery(), ct))))
            .RequireAuthorization("UserOrAdmin")
            .WithName("Charge.Catalog.PackageTypes")
            .WithTags("Charge.Catalog")
            .Produces<ApiResponse<IReadOnlyList<PackageTypeDto>>>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status401Unauthorized);
    }
}
