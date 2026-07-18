using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Refahi.Modules.Charge.Application.Contracts.Features;
using Refahi.Shared.Presentation;

namespace Refahi.Modules.Charge.Api.Endpoints.Admin;

public sealed class GetProviderCallLogsEndpoint : IEndpoint
{
    public void Map(object app)
    {
        if (app is not IEndpointRouteBuilder routes) return;
        routes.MapGet("admin/charge-requests/{id:guid}/provider-calls", async (
            Guid id, int page, int pageSize, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(new GetProviderCallLogsQuery(id, page <= 0 ? 1 : page, pageSize <= 0 ? 50 : pageSize), ct);
            return Results.Ok(ApiResponseHelper.Success(result));
        })
        .RequireAuthorization("AdminOnly")
        .WithName("Charge.Admin.GetProviderCalls")
        .WithTags("Charge.Admin")
        .Produces<ApiResponse<IReadOnlyList<ProviderCallLogDto>>>(StatusCodes.Status200OK);
    }
}
