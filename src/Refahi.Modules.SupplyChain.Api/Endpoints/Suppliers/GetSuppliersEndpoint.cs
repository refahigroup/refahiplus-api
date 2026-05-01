using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Refahi.Modules.SupplyChain.Application.Contracts.Dtos;
using Refahi.Modules.SupplyChain.Application.Contracts.Queries.Suppliers;
using Refahi.Shared.Presentation;

namespace Refahi.Modules.SupplyChain.Api.Endpoints.Suppliers;

public class GetSuppliersEndpoint : IEndpoint
{
    public void Map(object app)
    {
        if (app is not IEndpointRouteBuilder routes) return;

        routes.MapGet("/admin/suppliers", async (
            short? status,
            short? type,
            int? provinceId,
            string? search,
            int page,
            int size,
            IMediator mediator,
            CancellationToken ct) =>
        {
            var query = new GetSuppliersQuery(
                Status: status,
                Type: type,
                ProvinceId: provinceId,
                Search: search,
                Page: page > 0 ? page : 1,
                Size: size > 0 ? size : 20);

            var result = await mediator.Send(query, ct);
            return Results.Ok(ApiResponseHelper.SuccessPaginated(
                result.Data,
                result.PageNumber,
                result.PageSize,
                result.TotalCount));
        })
        .WithName("SupplyChain.GetSuppliers")
        .WithTags("SupplyChain.Suppliers")
        .RequireAuthorization("AdminOnly")
        .Produces<PaginatedResponse<SupplierListItemDto>>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden);
    }
}
