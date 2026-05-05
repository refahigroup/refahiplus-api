using FluentValidation;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Refahi.Modules.Identity.Application.Features.Addresses.UpdateAddress;
using Refahi.Modules.Identity.Domain.Exceptions;
using Refahi.Shared.Presentation;
using System.Security.Claims;

namespace Refahi.Modules.Identity.Api.Endpoints.Addresses;

public class UpdateAddressEndpoint : IEndpoint
{
    public sealed record UpdateAddressRequest(
        string Title,
        int ProvinceId,
        int CityId,
        string FullAddress,
        string PostalCode,
        string ReceiverName,
        string ReceiverPhone,
        string? Plate,
        string? Unit,
        double? Latitude,
        double? Longitude);

    public void Map(object app)
    {
        if (app is not IEndpointRouteBuilder routes) return;

        routes.MapPut("/addresses/{addressId:guid}", async (
            Guid addressId,
            [FromBody] UpdateAddressRequest body,
            HttpContext httpContext,
            IMediator mediator,
            IValidator<UpdateAddressCommand> validator,
            CancellationToken ct) =>
        {
            var userIdClaim = httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier)
                ?? httpContext.User.FindFirstValue("sub");

            if (userIdClaim is null || !Guid.TryParse(userIdClaim, out var userId))
                return Results.Unauthorized();

            var command = new UpdateAddressCommand(
                addressId,
                userId,
                body.Title,
                body.ProvinceId,
                body.CityId,
                body.FullAddress,
                body.PostalCode,
                body.ReceiverName,
                body.ReceiverPhone,
                body.Plate,
                body.Unit,
                body.Latitude,
                body.Longitude);

            var validation = await validator.ValidateAsync(command, ct);
            if (!validation.IsValid)
                return Results.BadRequest(ApiResponseHelper.Error(validation.Errors[0].ErrorMessage));

            try
            {
                var result = await mediator.Send(command, ct);
                return Results.Ok(ApiResponseHelper.Success(result, "آدرس با موفقیت ویرایش شد"));
            }
            catch (DomainException ex) when (ex.ErrorCode == "ADDRESS_NOT_FOUND")
            {
                return Results.NotFound(ApiResponseHelper.Error(ex.Message));
            }
        })
        .WithName("Identity.Addresses.Update")
        .WithTags("Identity.Addresses")
        .RequireAuthorization("UserOrAdmin")
        .Produces(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status404NotFound);
    }
}
