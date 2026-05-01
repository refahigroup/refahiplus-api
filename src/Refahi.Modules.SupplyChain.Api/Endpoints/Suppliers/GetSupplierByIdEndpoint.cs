using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Refahi.Modules.SupplyChain.Application.Contracts.Dtos;
using Refahi.Modules.SupplyChain.Application.Contracts.Queries.Suppliers;
using Refahi.Shared.Presentation;

namespace Refahi.Modules.SupplyChain.Api.Endpoints.Suppliers;

public class GetSupplierByIdEndpoint : IEndpoint
{
    public void Map(object app)
    {
        if (app is not IEndpointRouteBuilder routes) return;

        routes.MapGet("/admin/suppliers/{id:guid}", async (
            Guid id,
            IMediator mediator,
            CancellationToken ct) =>
        {
            var result = await mediator.Send(new GetSupplierByIdQuery(id), ct);
            if (result is null)
                return Results.NotFound();

            return Results.Ok(ApiResponseHelper.Success(result));
        })
        .WithName("SupplyChain.GetSupplierById")
        .WithTags("SupplyChain.Suppliers")
        .RequireAuthorization("AdminOnly")
        .Produces<ApiResponse<SupplierDto>>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden)
        .Produces(StatusCodes.Status404NotFound);
    }
}
