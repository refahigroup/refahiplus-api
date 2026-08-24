using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Refahi.Modules.Commerce.Application;
using Refahi.Modules.Commerce.Application.Contracts;
using Refahi.Modules.Commerce.Application.Features.Operations.GetOperation;
using Refahi.Shared.Presentation;

namespace Refahi.Modules.Commerce.Api.Endpoints.Admin;

public sealed class GetOperationsEndpoint : IEndpoint
{
    public void Map(object app)
    {
        if (app is not IEndpointRouteBuilder routes)
            return;

        routes.MapGet(
            "/admin/operations",
            async (
                string? status,
                int? pageNumber,
                int? pageSize,
                IMediator m,
                CancellationToken ct) =>
            {
                var result = await m.Send(new GetCommerceOperationsQuery(
                    status,
                    pageNumber is > 0 ? pageNumber.Value : 1,
                    pageSize is > 0 ? pageSize.Value : 50
                ), ct);

                return Results.Ok(ApiResponseHelper.Success(result));
            })
        .WithName("Commerce.Admin.GetOperations")
        .WithTags("Commerce.Admin").RequireAuthorization("AdminOnly");
    }
}

