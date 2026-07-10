using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Refahi.Modules.Charge.Application.Contracts.Features;
using Refahi.Shared.Presentation;

namespace Refahi.Modules.Charge.Api.Endpoints;

public sealed class GetMarkupRulesEndpoint : IEndpoint
{
    public void Map(object app)
    {
        if (app is not IEndpointRouteBuilder routes)
            return;

        routes.MapGet("admin/markup-rules", async (ISender sender, CancellationToken ct)
            => Results.Ok(ApiResponseHelper.Success(await sender.Send(new GetMarkupRulesQuery(), ct))))
            .RequireAuthorization("AdminOnly")
            .WithName("Charge.Admin.MarkupRules.Get")
            .WithTags("Charge.Admin")
            .Produces<ApiResponse<IReadOnlyList<MarkupRuleDto>>>(StatusCodes.Status200OK);
    }
}
