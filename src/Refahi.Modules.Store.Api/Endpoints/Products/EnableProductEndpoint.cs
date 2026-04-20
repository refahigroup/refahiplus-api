using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Refahi.Modules.Store.Application.Contracts.Commands.Products;
using Refahi.Shared.Presentation;

namespace Refahi.Modules.Store.Api.Endpoints.Products;

public class EnableProductEndpoint : IEndpoint
{
    public void Map(object app)
    {
        if (app is not IEndpointRouteBuilder routes) return;

        routes.MapPost("/admin/products/{id:guid}/enable", async (
            Guid id,
            IMediator mediator,
            CancellationToken ct) =>
        {
            var result = await mediator.Send(new EnableProductCommand(id), ct);
            return Results.Ok(ApiResponseHelper.Success(result, "محصول با موفقیت فعال شد"));
        })
        .WithName("Store.EnableProduct")
        .WithTags("Store.Products")
        .RequireAuthorization("AdminOnly")
        .Produces<ApiResponse<EnableProductResponse>>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status404NotFound);
    }
}
