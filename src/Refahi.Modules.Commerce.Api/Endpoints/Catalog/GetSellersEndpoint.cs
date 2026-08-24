using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Refahi.Modules.Commerce.Application;
using Refahi.Modules.Commerce.Application.Contracts;
using Refahi.Modules.Commerce.Application.Features.Catalog.GetSellers;
using Refahi.Shared.Presentation;

namespace Refahi.Modules.Commerce.Api.Endpoints.Catalog.Catalog;

public sealed class GetSellersEndpoint : IEndpoint
{
    public void Map(object app)
    {
        if (app is not IEndpointRouteBuilder routes)
            return;

        routes
            .MapGet("/catalog/sellers",
            async (
                IMediator m,
                CancellationToken ct
            ) => 
            {
                var result = await m.Send(new GetCommerceSellersQuery(), ct);

                return Results.Ok(ApiResponseHelper.Success(result));
            })
            .WithName("Commerce.GetSellers")
            .WithTags("Commerce.Catalog");
    }
}

