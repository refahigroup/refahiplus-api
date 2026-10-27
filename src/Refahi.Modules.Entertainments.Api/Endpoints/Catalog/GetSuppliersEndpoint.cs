using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Refahi.Modules.Entertainments.Application;
using Refahi.Modules.Entertainments.Application.Contracts;
using Refahi.Modules.Entertainments.Application.Features.Catalog.GetSuppliers;
using Refahi.Shared.Presentation;

namespace Refahi.Modules.Entertainments.Api.Endpoints.Catalog.Catalog;

public sealed class GetSuppliersEndpoint : IEndpoint
{
    public void Map(object app)
    {
        if (app is not IEndpointRouteBuilder routes)
            return;

        routes
            .MapGet("/suppliers",
            async (
                int? pageNumber,
                int? pageSize,
                string? providerKey,
                string? locationCode,
                string? categoryCode,
                IMediator m,
                CancellationToken ct
            ) => 
            {
                var result = await m.Send(new GetEntertainmentSuppliersQuery(
                    pageNumber is > 0 ? pageNumber.Value : 1,
                    pageSize is > 0 ? pageSize.Value : 24, providerKey, locationCode, categoryCode), ct);

                return Results.Ok(ApiResponseHelper.Success(result));
            })
            .WithName("Entertainment.GetSuppliers")
            .WithTags("Entertainment.Catalog");
    }
}

