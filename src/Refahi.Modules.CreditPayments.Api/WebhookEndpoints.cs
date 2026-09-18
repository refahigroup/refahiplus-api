using System.Security.Claims;
using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Refahi.Modules.CreditPayments.Application.Contracts;
using Refahi.Shared.Presentation;

namespace Refahi.Modules.CreditPayments.Api;

public sealed class WebhookAdminEndpoints : IEndpoint
{
    public void Map(object app)
    {
        if (app is not IEndpointRouteBuilder routes) return;
        routes.MapPost("/admin/webhook-events/{eventId:guid}/resend", async (
                Guid eventId, [FromBody] WebhookResendRequest request, ClaimsPrincipal user,
                IMediator mediator, CancellationToken ct) =>
            Results.Ok(ApiResponseHelper.Success(
                await mediator.Send(new ResendWebhookEventCommand(eventId,
                    user.FindFirstValue(ClaimTypes.NameIdentifier), request.Reason), ct),
                "ارسال مجدد وب‌هوک زمان‌بندی شد")))
            .WithName("CreditPayments.ResendWebhookEvent")
            .WithTags("CreditPayments.Admin.Webhooks")
            .RequireAuthorization("AdminOnly");
    }
}

public sealed record WebhookResendRequest(string? Reason);
