using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Refahi.Modules.Commerce.Application;
using Refahi.Modules.Commerce.Application.Contracts;
using Refahi.Modules.Commerce.Application.Features.Catalog.GetProduct;
using Refahi.Shared.Presentation;

namespace Refahi.Modules.Commerce.Api.Endpoints.Catalog;

public sealed class GetProductByProductKeyEndpoint : IEndpoint
{
    public void Map(object app)
    {
        if (app is not IEndpointRouteBuilder routes)
            return;

        routes
            .MapGet("/catalog/products/{providerKey}/{productKey}",
            async(
                string providerKey, 
                string productKey, 
                IMediator m,
                CancellationToken ct
            ) =>
            { 
                var value = await m.Send(new GetCommerceProductQuery(providerKey, productKey), ct);
                
                return value is null 
                    ? Results.NotFound(ApiResponseHelper.Error("محصول یافت نشد", statusCode: 404)) 
                    : Results.Ok(ApiResponseHelper.Success(value)); 
            })
            .WithName("Commerce.GetProduct")
            .WithTags("Commerce.Catalog");
    }
}

