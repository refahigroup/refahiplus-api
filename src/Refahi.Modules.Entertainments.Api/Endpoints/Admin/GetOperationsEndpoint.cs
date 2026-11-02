using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Refahi.Modules.Entertainments.Application;
using Refahi.Modules.Entertainments.Application.Contracts;
using Refahi.Modules.Entertainments.Application.Features.Operations.GetOperation;
using Refahi.Shared.Presentation;

namespace Refahi.Modules.Entertainments.Api.Endpoints.Admin;

public sealed class GetOperationsEndpoint : IEndpoint
{
    public void Map(object app)
    {
        if (app is not IEndpointRouteBuilder routes)
            return;

        routes.MapGet("/admin/operations/{id:guid}", async (Guid id, IMediator sender, CancellationToken ct) => {
            var value = await sender.Send(new GetEntertainmentOperationQuery(id), ct);
            return value is null ? Results.NotFound(ApiResponseHelper.Error("عملیات یافت نشد", statusCode: 404)) : Results.Ok(ApiResponseHelper.Success(value));
        }).RequireAuthorization("AdminOnly").WithName("Entertainments.OperationDetail").WithTags("Entertainments.Admin");
        routes.MapGet(
            "/admin/operations",
            async (
                string? status,
                int? pageNumber,
                int? pageSize,
                IMediator m,
                CancellationToken ct) =>
            {
                var result = await m.Send(new GetEntertainmentOperationsQuery(
                    status,
                    pageNumber is > 0 ? pageNumber.Value : 1,
                    pageSize is > 0 ? pageSize.Value : 50
                ), ct);

                return Results.Ok(ApiResponseHelper.Success(result));
            })
        .WithName("Entertainment.Admin.GetOperations")
        .WithTags("Entertainment.Admin").RequireAuthorization("AdminOnly");
    }
}

