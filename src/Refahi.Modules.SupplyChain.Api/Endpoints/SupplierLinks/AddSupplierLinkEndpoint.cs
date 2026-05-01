using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Refahi.Modules.SupplyChain.Application.Contracts.Commands.SupplierLinks;
using Refahi.Shared.Presentation;

namespace Refahi.Modules.SupplyChain.Api.Endpoints.SupplierLinks;

public class AddSupplierLinkEndpoint : IEndpoint
{
    public void Map(object app)
    {
        if (app is not IEndpointRouteBuilder routes) return;

        routes.MapPost("/admin/suppliers/{id:guid}/links", async (
            Guid id,
            [FromBody] AddSupplierLinkRequest body,
            IMediator mediator,
            CancellationToken ct) =>
        {
            var command = new AddSupplierLinkCommand(id, body.Type, body.Url, body.Label);
            var result = await mediator.Send(command, ct);
            return Results.Created(
                $"/api/supply-chain/admin/suppliers/{id}/links/{result.LinkId}",
                ApiResponseHelper.Success(result, "لینک با موفقیت اضافه شد", 201));
        })
        .WithName("SupplyChain.AddSupplierLink")
        .WithTags("SupplyChain.SupplierLinks")
        .RequireAuthorization("AdminOnly")
        .Produces<ApiResponse<AddSupplierLinkResponse>>(StatusCodes.Status201Created)
        .Produces<ApiErrorResponse>(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden)
        .Produces(StatusCodes.Status404NotFound);
    }
}

public sealed record AddSupplierLinkRequest(short Type, string Url, string? Label);
