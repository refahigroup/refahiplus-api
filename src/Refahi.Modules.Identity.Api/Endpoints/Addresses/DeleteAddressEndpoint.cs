using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Refahi.Modules.Identity.Application.Features.Addresses.DeleteAddress;
using Refahi.Modules.Identity.Domain.Exceptions;
using Refahi.Shared.Presentation;
using System.Security.Claims;

namespace Refahi.Modules.Identity.Api.Endpoints.Addresses;

public class DeleteAddressEndpoint : IEndpoint
{
    public void Map(object app)
    {
        if (app is not IEndpointRouteBuilder routes) return;

        routes.MapDelete("/addresses/{addressId:guid}", async (
            Guid addressId,
            HttpContext httpContext,
            IMediator mediator,
            CancellationToken ct) =>
        {
            var userIdClaim = httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier)
                ?? httpContext.User.FindFirstValue("sub");

            if (userIdClaim is null || !Guid.TryParse(userIdClaim, out var userId))
                return Results.Unauthorized();

            try
            {
                await mediator.Send(new DeleteAddressCommand(addressId, userId), ct);
                return Results.Ok(ApiResponseHelper.Success<object?>(null, "آدرس حذف شد"));
            }
            catch (DomainException ex) when (ex.ErrorCode == "ADDRESS_NOT_FOUND")
            {
                return Results.NotFound(ApiResponseHelper.Error(ex.Message));
            }
        })
        .WithName("Identity.Addresses.Delete")
        .WithTags("Identity.Addresses")
        .RequireAuthorization("UserOrAdmin")
        .Produces(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status404NotFound);
    }
}
