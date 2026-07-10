using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Refahi.Modules.Charge.Application.Contracts.Features;
using Refahi.Shared.Presentation;

namespace Refahi.Modules.Charge.Api.Endpoints;

public sealed class UpdateMarkupRuleEndpoint : IEndpoint
{
    public void Map(object app)
    {
        if (app is not IEndpointRouteBuilder routes)
            return;

        routes.MapPut("admin/markup-rules/{id:guid}", async (Guid id, [FromBody] MarkupRuleBody body, ISender sender, CancellationToken ct)
            => Results.Ok(ApiResponseHelper.Success(
                await sender.Send(new UpsertMarkupRuleCommand(
                    id,
                    body.Operator,
                    body.ServiceType,
                    body.Percent,
                    body.FixedAmountMinor,
                    body.EffectiveFrom,
                    body.EffectiveTo), ct),
                "قانون افزایش قیمت ویرایش شد")))
            .RequireAuthorization("AdminOnly")
            .WithName("Charge.Admin.MarkupRules.Update")
            .WithTags("Charge.Admin")
            .Produces<ApiResponse<MarkupRuleDto>>(StatusCodes.Status200OK);
    }
}
