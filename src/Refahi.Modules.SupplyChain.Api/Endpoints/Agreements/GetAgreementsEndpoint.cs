using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Refahi.Modules.SupplyChain.Application.Contracts.Dtos;
using Refahi.Modules.SupplyChain.Application.Contracts.Queries.Agreements;
using Refahi.Shared.Presentation;

namespace Refahi.Modules.SupplyChain.Api.Endpoints.Agreements;

public class GetAgreementsEndpoint : IEndpoint
{
    public void Map(object app)
    {
        if (app is not IEndpointRouteBuilder routes) return;

        routes.MapGet("/admin/agreements", async (
            Guid? supplierId,
            short? status,
            short? type,
            string? search,
            int page,
            int size,
            IMediator mediator,
            CancellationToken ct) =>
        {
            var query = new GetAgreementsQuery(
                SupplierId: supplierId,
                Status: status,
                Type: type,
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
        .WithName("SupplyChain.GetAgreements")
        .WithTags("SupplyChain.Agreements")
        .RequireAuthorization("AdminOnly")
        .Produces<PaginatedResponse<AgreementListItemDto>>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status403Forbidden);
    }
}
