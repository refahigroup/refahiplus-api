using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Refahi.Modules.SupplyChain.Application.Contracts.Commands.Suppliers;
using Refahi.Shared.Presentation;

namespace Refahi.Modules.SupplyChain.Api.Endpoints.Suppliers;

public class UpdateSupplierEndpoint : IEndpoint
{
    public void Map(object app)
    {
        if (app is not IEndpointRouteBuilder routes) return;

        routes.MapPut("/admin/suppliers/{id:guid}", async (
            Guid id,
            [FromBody] UpdateSupplierRequest body,
            IMediator mediator,
            CancellationToken ct) =>
        {
            var command = new UpdateSupplierCommand(
                id,
                body.FirstName, body.LastName,
                body.CompanyName, body.BrandName,
                body.LogoUrl,
                body.NationalId, body.EconomicCode,
                body.ProvinceId, body.CityId,
                body.Address, body.Latitude, body.Longitude,
                body.MobileNumber, body.PhoneNumber,
                body.RepresentativeName, body.RepresentativePhone);

            await mediator.Send(command, ct);
            return Results.Ok(ApiResponseHelper.Success<object>(null!, "تامین‌کننده با موفقیت بروزرسانی شد"));
        })
        .WithName("SupplyChain.UpdateSupplier")
        .WithTags("SupplyChain.Suppliers")
        .RequireAuthorization("AdminOnly")
        .Produces(StatusCodes.Status200OK)
        .Produces<ApiErrorResponse>(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden)
        .Produces(StatusCodes.Status404NotFound);
    }
}

public sealed record UpdateSupplierRequest(
    string? FirstName, string? LastName,
    string? CompanyName, string? BrandName,
    string? LogoUrl,
    string? NationalId, string? EconomicCode,
    int? ProvinceId, int? CityId,
    string? Address, double? Latitude, double? Longitude,
    string? MobileNumber, string? PhoneNumber,
    string? RepresentativeName, string? RepresentativePhone);
