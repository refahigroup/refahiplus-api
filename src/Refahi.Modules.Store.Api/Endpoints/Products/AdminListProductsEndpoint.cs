using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Refahi.Modules.Store.Application.Contracts.Queries.Products;
using Refahi.Shared.Presentation;

namespace Refahi.Modules.Store.Api.Endpoints.Products;

public class AdminListProductsEndpoint : IEndpoint
{
    public void Map(object app)
    {
        if (app is not IEndpointRouteBuilder routes) return;

        routes.MapGet("/admin/products", async (
            Guid? shopId,
            bool? isDeleted,
            int pageNumber,
            int pageSize,
            IMediator mediator,
            CancellationToken ct) =>
        {
            var query = new AdminGetProductsQuery(
                shopId,
                isDeleted,
                pageNumber <= 0 ? 1 : pageNumber,
                pageSize <= 0 ? 20 : pageSize);

            var result = await mediator.Send(query, ct);
            return Results.Ok(ApiResponseHelper.SuccessPaginated(
                result.Data, result.PageNumber, result.PageSize, result.TotalCount));
        })
        .WithName("Store.Admin.ListProducts")
        .WithTags("Store.Products")
        .RequireAuthorization("AdminOnly")
        .Produces<ApiResponse<AdminProductsPagedResponse>>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden);
    }
}
