using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Refahi.Modules.Commerce.Application;
using Refahi.Modules.Commerce.Application.Contracts;
using Refahi.Modules.Commerce.Application.Features.Catalog.GetProducts;
using Refahi.Shared.Presentation;

namespace Refahi.Modules.Commerce.Api.Endpoints.Catalog;

public sealed class GetProductsEndpoint : IEndpoint
{
    public void Map(object app)
    {
        if (app is not IEndpointRouteBuilder routes)
            return;

        routes
            .MapGet("/catalog/products",
            async (
                string? search, 
                string? providerKey, 
                int? pageNumber, 
                int? pageSize, 
                IMediator m, 
                CancellationToken ct
            ) =>
            {
                var result = await m.Send(new GetCommerceProductsQuery(
                    search,
                    providerKey,
                    pageNumber is > 0 ? pageNumber.Value : 1,
                    pageSize is > 0 ? pageSize.Value : 24
                ), ct);

                return Results.Ok(ApiResponseHelper.Success(result));
            })
            .WithName("Commerce.GetProducts")
            .WithTags("Commerce.Catalog");
    }
}

