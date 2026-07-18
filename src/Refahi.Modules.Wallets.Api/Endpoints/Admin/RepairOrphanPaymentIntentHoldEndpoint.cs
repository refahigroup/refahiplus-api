using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Refahi.Modules.Wallets.Application.Contracts.Commands;
using Refahi.Modules.Wallets.Application.Contracts.Infrastructure;
using Refahi.Shared.Presentation;
using System;

namespace Refahi.Modules.Wallets.Api.Endpoints.Admin;

public sealed record RepairOrphanHoldBody(Guid ExpectedOrderId, bool DryRun, string IdempotencyKey);

public sealed class RepairOrphanPaymentIntentHoldEndpoint : IEndpoint
{
    public void Map(object app)
    {
        if (app is not IEndpointRouteBuilder routes) return;
        routes.MapPost("/admin/payment-intents/{intentId:guid}/repair-orphan-hold", async (
            Guid intentId, RepairOrphanHoldBody body, ISender sender, CancellationToken ct) =>
        {
            var result = await sender.Send(new RepairOrphanPaymentIntentHoldCommand(
                intentId, body.ExpectedOrderId, body.DryRun, body.IdempotencyKey), ct);
            return Results.Ok(ApiResponseHelper.Success(result,
                body.DryRun ? "بررسی بدون تغییر انجام شد" : "ترمیم رزرو انجام شد"));
        })
        .RequireAuthorization("AdminOnly")
        .WithName("Wallets.Admin.RepairOrphanPaymentIntentHold")
        .WithTags("Wallets.Admin")
        .Produces<ApiResponse<OrphanHoldRepairResult>>(StatusCodes.Status200OK);
    }
}
