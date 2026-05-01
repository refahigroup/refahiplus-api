using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Refahi.Modules.SupplyChain.Application.Contracts.Commands.Suppliers;
using Refahi.Shared.Presentation;

namespace Refahi.Modules.SupplyChain.Api.Endpoints.Suppliers;

public class CreateSupplierEndpoint : IEndpoint
{
    public void Map(object app)
    {
        if (app is not IEndpointRouteBuilder routes) return;

        routes.MapPost("/admin/suppliers", async (
            [FromBody] CreateSupplierCommand command,
            IMediator mediator,
            CancellationToken ct) =>
        {
            var result = await mediator.Send(command, ct);
            return Results.Created(
                $"/api/supply-chain/admin/suppliers/{result.Id}",
                ApiResponseHelper.Success(result, "تامین‌کننده با موفقیت ایجاد شد", 201));
        })
        .WithName("SupplyChain.CreateSupplier")
        .WithTags("SupplyChain.Suppliers")
        .RequireAuthorization("AdminOnly")
        .Produces<ApiResponse<CreateSupplierResponse>>(StatusCodes.Status201Created)
        .Produces<ApiErrorResponse>(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden);
    }
}
