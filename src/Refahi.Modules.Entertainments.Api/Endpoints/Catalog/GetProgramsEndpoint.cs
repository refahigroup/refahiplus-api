using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Refahi.Modules.Entertainments.Application;
using Refahi.Modules.Entertainments.Application.Contracts;
using Refahi.Modules.Entertainments.Application.Features.Catalog.GetPrograms;
using Refahi.Shared.Presentation;

namespace Refahi.Modules.Entertainments.Api.Endpoints.Catalog;

public sealed class GetProgramsEndpoint : IEndpoint
{
    public void Map(object app)
    {
        if (app is not IEndpointRouteBuilder routes)
            return;

        routes
            .MapGet("/programs",
            async (
                string? search, 
                string? providerKey, 
                string? supplierKey,
                string? locationCode,
                string? categoryCode,
                int? pageNumber, 
                int? pageSize, 
                IMediator m, 
                CancellationToken ct
            ) =>
            {
                var result = await m.Send(new GetEntertainmentProgramsQuery(
                    search,
                    providerKey,
                    supplierKey,
                    pageNumber is > 0 ? pageNumber.Value : 1,
                    pageSize is > 0 ? pageSize.Value : 24,
                    locationCode,
                    categoryCode
                ), ct);

                return Results.Ok(ApiResponseHelper.Success(result));
            })
            .WithName("Entertainment.GetPrograms")
            .WithTags("Entertainment.Catalog");
    }
}

