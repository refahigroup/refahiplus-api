using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Refahi.Modules.Charge.Application.Contracts.Features;
using Refahi.Shared.Presentation;

namespace Refahi.Modules.Charge.Api.Endpoints;

public sealed class CreateMarkupRuleEndpoint : IEndpoint
{
    public void Map(object app)
    {
        if (app is not IEndpointRouteBuilder routes)
            return;

        routes.MapPost("admin/markup-rules", async ([FromBody] MarkupRuleBody body, ISender sender, CancellationToken ct)
            => Results.Ok(ApiResponseHelper.Success(
                await sender.Send(new UpsertMarkupRuleCommand(
                    null,
                    body.Operator,
                    body.ServiceType,
                    body.Percent,
                    body.FixedAmountMinor,
                    body.EffectiveFrom,
                    body.EffectiveTo), ct),
                "قانون افزایش قیمت ثبت شد")))
            .RequireAuthorization("AdminOnly")
            .WithName("Charge.Admin.MarkupRules.Create")
            .WithTags("Charge.Admin")
            .Produces<ApiResponse<MarkupRuleDto>>(StatusCodes.Status200OK);
    }
}
