using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Refahi.Modules.Entertainments.Application.Features.Catalog.Offers;
using Refahi.Shared.Presentation;
namespace Refahi.Modules.Entertainments.Api.Endpoints.Catalog;

public sealed class GetEntertainmentOffersEndpoint : IEndpoint
{
    public void Map(object app)
    {
        if (app is not IEndpointRouteBuilder routes) return;
        routes.MapGet("/programs/{programKey:guid}/requirements", async (string programKey, IMediator sender, CancellationToken ct) =>
            Results.Ok(ApiResponseHelper.Success(await sender.Send(new GetEntertainmentRequirementsQuery(programKey), ct))))
            .WithName("Entertainments.PurchaseRequirements").WithTags("Entertainments.Offers");
        routes.MapGet("/programs/{programKey:guid}/offers", async (string programKey, DateOnly? start, DateOnly? end, IMediator m, CancellationToken ct) =>
            Results.Ok(ApiResponseHelper.Success(await m.Send(new GetEntertainmentOffersQuery("touristpanel", programKey, start, end), ct))))
            .WithName("Entertainment.GetOffers").WithTags("Entertainment.Catalog");
        routes.MapGet("/filters/{providerKey}", async (string providerKey, IMediator m, CancellationToken ct) =>
            Results.Ok(ApiResponseHelper.Success(await m.Send(new GetEntertainmentFiltersQuery(providerKey), ct))))
            .WithName("Entertainment.GetFilters").WithTags("Entertainment.Catalog");
        routes.MapGet("/filters", async (string? providerKey, IMediator m, CancellationToken ct) =>
            Results.Ok(ApiResponseHelper.Success(await m.Send(new GetEntertainmentFiltersQuery(providerKey), ct))))
            .WithName("Entertainment.GetCombinedFilters").WithTags("Entertainment.Catalog");
    }
}
