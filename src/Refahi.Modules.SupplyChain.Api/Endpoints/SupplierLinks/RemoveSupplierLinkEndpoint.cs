using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Refahi.Modules.SupplyChain.Application.Contracts.Commands.SupplierLinks;
using Refahi.Shared.Presentation;

namespace Refahi.Modules.SupplyChain.Api.Endpoints.SupplierLinks;

public class RemoveSupplierLinkEndpoint : IEndpoint
{
    public void Map(object app)
    {
        if (app is not IEndpointRouteBuilder routes) return;

        routes.MapDelete("/admin/suppliers/{id:guid}/links/{linkId:guid}", async (
            Guid id,
            Guid linkId,
            IMediator mediator,
            CancellationToken ct) =>
        {
            await mediator.Send(new RemoveSupplierLinkCommand(id, linkId), ct);
            return Results.NoContent();
        })
        .WithName("SupplyChain.RemoveSupplierLink")
        .WithTags("SupplyChain.SupplierLinks")
        .RequireAuthorization("AdminOnly")
        .Produces(StatusCodes.Status204NoContent)
        .Produces<ApiErrorResponse>(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden)
        .Produces(StatusCodes.Status404NotFound);
    }
}
