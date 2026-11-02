using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Refahi.Modules.Entertainments.Application.Features.Catalog.GetPrograms;
using Refahi.Modules.Entertainments.Application.Features.Catalog.GetProgram;
using Refahi.Modules.Entertainments.Application.Features.Catalog.GetSuppliers;
using Refahi.Modules.Entertainments.Application.Features.Catalog.GetSupplier;
using Refahi.Modules.Entertainments.Application.Features.Catalog.Offers;
using Refahi.Modules.Entertainments.Application.Features.Customer;
using Refahi.Shared.Presentation;
namespace Refahi.Modules.Entertainments.Api.Endpoints.Catalog;

public sealed class GetProgramsEndpoint : IEndpoint
{
    public void Map(object app)
    {
        if (app is not IEndpointRouteBuilder routes) return;
        routes.MapGet("/programs", async (string? search, string? supplierKey, string? locationCode, string? categoryCode, int? pageNumber, int? pageSize, IMediator m, CancellationToken ct) =>
            Results.Ok(ApiResponseHelper.Success(await m.Send(new GetEntertainmentProgramsQuery(search, supplierKey, pageNumber ?? 1, pageSize ?? 24, locationCode, categoryCode), ct))))
            .WithName("Entertainment.GetPrograms").WithTags("Entertainment.Content");
        routes.MapGet("/programs/{programKey:guid}", async (Guid programKey, IMediator m, CancellationToken ct) => {
            var value = await m.Send(new GetEntertainmentProgramQuery(programKey.ToString("N")), ct);
            return value is null ? Results.NotFound(ApiResponseHelper.Error("خدمت یافت نشد", statusCode: 404)) : Results.Ok(ApiResponseHelper.Success(value));
        }).WithName("Entertainment.GetProgram").WithTags("Entertainment.Content");
        routes.MapGet("/suppliers", async (int? pageNumber, int? pageSize, string? locationCode, string? categoryCode, IMediator m, CancellationToken ct) =>
            Results.Ok(ApiResponseHelper.Success(await m.Send(new GetEntertainmentSuppliersQuery(pageNumber ?? 1, pageSize ?? 24, locationCode, categoryCode), ct))))
            .WithName("Entertainment.GetSuppliers").WithTags("Entertainment.Content");
        routes.MapGet("/suppliers/{supplierKey:guid}", async (Guid supplierKey, IMediator m, CancellationToken ct) => {
            var value = await m.Send(new GetEntertainmentSupplierQuery(supplierKey), ct);
            return value is null ? Results.NotFound(ApiResponseHelper.Error("مجموعه یافت نشد", statusCode: 404)) : Results.Ok(ApiResponseHelper.Success(value));
        }).WithName("Entertainment.GetSupplier").WithTags("Entertainment.Content");
        routes.MapGet("/filters", async (IMediator m, CancellationToken ct) => Results.Ok(ApiResponseHelper.Success(await m.Send(new GetEntertainmentFiltersQuery(), ct))))
            .WithName("Entertainment.GetFilters").WithTags("Entertainment.Content");
        routes.MapGet("/programs/{programKey:guid}/requirements", async (Guid programKey, IMediator m, CancellationToken ct) =>
            Results.Ok(ApiResponseHelper.Success(await m.Send(new GetEntertainmentRequirementsQuery(programKey.ToString("N")), ct))))
            .WithName("Entertainments.PurchaseRequirements").WithTags("Entertainment.Offers");
        routes.MapGet("/programs/{programKey:guid}/offers", async (Guid programKey, DateOnly? start, DateOnly? end, IMediator m, CancellationToken ct) =>
            Results.Ok(ApiResponseHelper.Success((await m.Send(new GetEntertainmentOffersQuery(programKey.ToString("N"), start, end), ct)).Select(x => x.Public()).ToArray())))
            .WithName("Entertainment.GetOffers").WithTags("Entertainment.Offers");
    }
}
