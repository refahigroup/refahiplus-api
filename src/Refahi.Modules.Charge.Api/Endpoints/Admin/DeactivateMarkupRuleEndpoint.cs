using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Refahi.Modules.Charge.Application.Contracts.Features;
using Refahi.Shared.Presentation;

namespace Refahi.Modules.Charge.Api.Endpoints;

public sealed class DeactivateMarkupRuleEndpoint : IEndpoint
{
    public void Map(object app)
    {
        if (app is not IEndpointRouteBuilder routes)
            return;

        routes.MapPost("admin/markup-rules/{id:guid}/deactivate", async (Guid id, ISender sender, CancellationToken ct) =>
        {
            await sender.Send(new DeactivateMarkupRuleCommand(id), ct);
            return Results.Ok(ApiResponseHelper.Success(new ChargeOperationResponse(id), "قانون افزایش قیمت غیرفعال شد"));
        })
        .RequireAuthorization("AdminOnly")
        .WithName("Charge.Admin.MarkupRules.Deactivate")
        .WithTags("Charge.Admin")
        .Produces<ApiResponse<ChargeOperationResponse>>(StatusCodes.Status200OK);
    }
}
