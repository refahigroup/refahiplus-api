using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Refahi.Modules.Store.Application.Contracts.Commands.Shops;
using Refahi.Shared.Presentation;

namespace Refahi.Modules.Store.Api.Endpoints.Shops;

public class AdminUpdateShopEndpoint : IEndpoint
{
    public void Map(object app)
    {
        if (app is not IEndpointRouteBuilder routes) return;

        routes.MapPut("/admin/shops/{id:guid}", async (
            Guid id,
            [FromBody] AdminUpdateShopRequest request,
            IMediator mediator,
            CancellationToken ct) =>
        {
            var command = new UpdateShopCommand(
                id,
                request.Name,
                request.Description,
                request.ProvinceId,
                request.CityId,
                request.Address,
                request.Latitude,
                request.Longitude,
                request.ManagerName,
                request.ManagerPhone,
                request.RepresentativeName,
                request.RepresentativePhone,
                request.ContactPhone,
                request.LogoUrl,
                request.CoverImageUrl);

            var result = await mediator.Send(command, ct);
            return Results.Ok(ApiResponseHelper.Success(result, "اطلاعات فروشگاه با موفقیت بروزرسانی شد"));
        })
        .WithName("Store.Admin.UpdateShop")
        .WithTags("Store.Shops")
        .RequireAuthorization("AdminOnly")
        .Produces<ApiResponse<UpdateShopResponse>>(StatusCodes.Status200OK)
        .Produces<ApiErrorResponse>(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden)
        .Produces(StatusCodes.Status404NotFound);
    }
}

public sealed record AdminUpdateShopRequest(
    string Name,
    string? Description,
    int? ProvinceId,
    int? CityId,
    string? Address,
    double? Latitude,
    double? Longitude,
    string? ManagerName,
    string? ManagerPhone,
    string? RepresentativeName,
    string? RepresentativePhone,
    string? ContactPhone,
    string? LogoUrl,
    string? CoverImageUrl);
