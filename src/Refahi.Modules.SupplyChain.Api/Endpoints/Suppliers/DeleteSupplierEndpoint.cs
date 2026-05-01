using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Refahi.Modules.SupplyChain.Application.Contracts.Commands.Suppliers;
using Refahi.Shared.Presentation;

namespace Refahi.Modules.SupplyChain.Api.Endpoints.Suppliers;

public class DeleteSupplierEndpoint : IEndpoint
{
    public void Map(object app)
    {
        if (app is not IEndpointRouteBuilder routes) return;

        routes.MapDelete("/admin/suppliers/{id:guid}", async (
            Guid id,
            IMediator mediator,
            CancellationToken ct) =>
        {
            await mediator.Send(new DeleteSupplierCommand(id), ct);
            return Results.NoContent();
        })
        .WithName("SupplyChain.DeleteSupplier")
        .WithTags("SupplyChain.Suppliers")
        .RequireAuthorization("AdminOnly")
        .Produces(StatusCodes.Status204NoContent)
        .Produces<ApiErrorResponse>(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden)
        .Produces(StatusCodes.Status404NotFound);
    }
}
