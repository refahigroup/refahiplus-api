using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Refahi.Modules.Charge.Application.Contracts.Features;
using Refahi.Shared.Presentation;

namespace Refahi.Modules.Charge.Api.Endpoints.Admin;

public sealed record ConfirmFulfilledBody(string ProviderRrn, string ProviderTraceId, string Evidence);
public sealed record RefundChargeBody(string Reason, string IdempotencyKey);

public sealed class TraceChargeAgainEndpoint : IEndpoint
{
    public void Map(object app)
    {
        if (app is not IEndpointRouteBuilder routes) return;
        routes.MapPost("admin/charge-requests/{id:guid}/trace-again", async (Guid id, ISender sender, CancellationToken ct) =>
            Results.Ok(ApiResponseHelper.Success(await sender.Send(new ReconcileChargeRequestCommand(id, true), ct))))
            .RequireAuthorization("AdminOnly").WithName("Charge.Admin.TraceAgain").WithTags("Charge.Admin")
            .Produces<ApiResponse<ReconcileChargeRequestResponse>>(StatusCodes.Status200OK);
    }
}

public sealed class ConfirmChargeFulfilledEndpoint : IEndpoint
{
    public void Map(object app)
    {
        if (app is not IEndpointRouteBuilder routes) return;
        routes.MapPost("admin/charge-requests/{id:guid}/confirm-fulfilled", async (Guid id, ConfirmFulfilledBody body, ISender sender, CancellationToken ct) =>
            Results.Ok(ApiResponseHelper.Success(await sender.Send(new ConfirmChargeFulfilledCommand(id, body.ProviderRrn, body.ProviderTraceId, body.Evidence), ct))))
            .RequireAuthorization("AdminOnly").WithName("Charge.Admin.ConfirmFulfilled").WithTags("Charge.Admin")
            .Produces<ApiResponse<ReconcileChargeRequestResponse>>(StatusCodes.Status200OK);
    }
}

public sealed class RefundChargeRequestEndpoint : IEndpoint
{
    public void Map(object app)
    {
        if (app is not IEndpointRouteBuilder routes) return;
        routes.MapPost("admin/charge-requests/{id:guid}/refund", async (Guid id, RefundChargeBody body, ISender sender, CancellationToken ct) =>
            Results.Ok(ApiResponseHelper.Success(await sender.Send(new RefundChargeRequestCommand(id, body.Reason, body.IdempotencyKey), ct))))
            .RequireAuthorization("AdminOnly").WithName("Charge.Admin.Refund").WithTags("Charge.Admin")
            .Produces<ApiResponse<ReconcileChargeRequestResponse>>(StatusCodes.Status200OK);
    }
}
