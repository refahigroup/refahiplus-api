using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Refahi.Modules.Commerce.Application.Features.Catalog.Offers;
using Refahi.Shared.Presentation;
namespace Refahi.Modules.Commerce.Api.Endpoints.Catalog;

public sealed class GetCommerceOffersEndpoint : IEndpoint
{
    public void Map(object app)
    {
        if (app is not IEndpointRouteBuilder routes) return;
        routes.MapGet("/catalog/products/{providerKey}/{productKey}/offers", async (string providerKey, string productKey, DateOnly? start, DateOnly? end, IMediator m, CancellationToken ct) =>
            Results.Ok(ApiResponseHelper.Success(await m.Send(new GetCommerceOffersQuery(providerKey, productKey, start, end), ct))))
            .WithName("Commerce.GetOffers").WithTags("Commerce.Catalog");
        routes.MapGet("/catalog/filters/{providerKey}", async (string providerKey, IMediator m, CancellationToken ct) =>
            Results.Ok(ApiResponseHelper.Success(await m.Send(new GetCommerceFiltersQuery(providerKey), ct))))
            .WithName("Commerce.GetFilters").WithTags("Commerce.Catalog");
        routes.MapGet("/catalog/filters", async (string? providerKey, IMediator m, CancellationToken ct) =>
            Results.Ok(ApiResponseHelper.Success(await m.Send(new GetCommerceFiltersQuery(providerKey), ct))))
            .WithName("Commerce.GetCombinedFilters").WithTags("Commerce.Catalog");
    }
}
