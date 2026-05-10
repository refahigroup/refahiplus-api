using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Refahi.Modules.PaymentGateway.Application.Contracts.Features.ProcessCallback;
using Refahi.Modules.PaymentGateway.Domain.Enums;
using Refahi.Shared.Presentation;
using System;
using System.Text.Json;

namespace Refahi.Modules.PaymentGateway.Api.Endpoints;

/// <summary>
/// POST /api/payment-gateway/callback/jibit/{sessionId}
/// بازگشت از درگاه پرداخت جیبیت — هیچ احراز هویتی ندارد (external POST از جیبیت)
///
/// جیبیت پس از پرداخت، به این آدرس POST می‌کند و فیلدهای زیر را ارسال می‌کند:
///   purchaseId — شناسه تراکنش جیبیت (برای Verify استفاده می‌شود)
///   status  — وضعیت: SUCCESSFUL / FAILED
///   amount  — مبلغ به ریال
///
/// sessionId در URL آدرس است زیرا جیبیت clientReferenceNumber را در callback برنمی‌گرداند.
/// این آدرس توسط JibitPaymentGatewayProvider.GetTokenAsync به callbackUrl اضافه می‌شود.
/// </summary>
public class JibitCallbackEndpoint : IEndpoint
{
    public void Map(object app)
    {
        if (app is not IEndpointRouteBuilder routes) return;

        routes.MapPost("/callback/jibit/{sessionId:guid}", async (
            Guid sessionId,
            HttpRequest request,
            IMediator mediator,
            CancellationToken ct) =>
        {
            // Jibit sends a form POST
            var form = await request.ReadFormAsync(ct);

            var purchaseId = form["purchaseId"].ToString();
            var status = form["status"].ToString();
            var amountStr = form["amount"].ToString();

            // Serialize raw form data for audit logging
            var rawJson = JsonSerializer.Serialize(form.ToDictionary(
                kvp => kvp.Key,
                kvp => kvp.Value.ToString()));

            if (string.IsNullOrWhiteSpace(purchaseId))
            {
                return Results.BadRequest("purchaseId الزامی است.");
            }

            // Normalize Jibit status → the handler uses "OK" to detect success
            var normalizedState = status.Equals("SUCCESSFUL", StringComparison.OrdinalIgnoreCase)
                ? "OK"
                : status;

            var command = new ProcessCallbackCommand(
                Provider: PaymentGatewayProviderType.Jibit,
                State: normalizedState,
                RefNum: purchaseId,   // purchaseId used for VerifyAsync
                ResNum: sessionId.ToString(),  // our session ID
                TraceNo: null,
                SecurePan: null,
                RawCallbackJson: rawJson);

            var response = await mediator.Send(command, ct);

            // Redirect the browser to the Blazor result page
            return Results.Redirect(response.BrowserRedirectUrl);
        })
        // ⚠️ No authentication — this is called by Jibit's server
        .WithName("PaymentGateway.JibitCallback")
        .WithTags("PaymentGateway")
        .ExcludeFromDescription();
    }
}
