using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Refahi.Modules.PaymentGateway.Application.Contracts.Features.ProcessCallback;
using Refahi.Modules.PaymentGateway.Domain.Enums;
using Refahi.Shared.Presentation;
using System.Text.Json;

namespace Refahi.Modules.PaymentGateway.Api.Endpoints;

/// <summary>
/// POST /api/payment-gateway/callback/{provider}
/// بازگشت از درگاه پرداخت — این endpoint هیچ احراز هویتی ندارد (external POST از SEP)
/// پس از تأیید تراکنش، مرورگر کاربر را به صفحه نتیجه redirect می‌کند.
/// </summary>
public class PaymentCallbackEndpoint : IEndpoint
{
    public void Map(object app)
    {
        if (app is not IEndpointRouteBuilder routes) return;

        routes.MapPost("/callback/{provider}", async (
            string provider,
            HttpRequest request,
            IMediator mediator,
            CancellationToken ct) =>
        {
            // Parse provider type
            if (!Enum.TryParse<PaymentGatewayProviderType>(provider, ignoreCase: true, out var providerType))
                return Results.BadRequest($"درگاه پرداخت '{provider}' پشتیبانی نمی‌شود.");

            // SEP sends a form POST — read form fields
            var form = await request.ReadFormAsync(ct);

            var state = form["state"].ToString();
            var refNum = form["RefNum"].ToString();
            var resNum = form["ResNum"].ToString();
            var traceNo = form["TraceNo"].ToString();
            var securePan = form["SecurePan"].ToString();

            // Serialize raw form data for audit logging
            var rawJson = JsonSerializer.Serialize(form.ToDictionary(
                kvp => kvp.Key,
                kvp => kvp.Value.ToString()));

            if (string.IsNullOrWhiteSpace(resNum))
            {
                // Cannot redirect without a session — return an error page
                return Results.BadRequest("ResNum الزامی است.");
            }

            var command = new ProcessCallbackCommand(
                Provider: providerType,
                State: state,
                RefNum: string.IsNullOrWhiteSpace(refNum) ? null : refNum,
                ResNum: resNum,
                TraceNo: string.IsNullOrWhiteSpace(traceNo) ? null : traceNo,
                SecurePan: string.IsNullOrWhiteSpace(securePan) ? null : securePan,
                RawCallbackJson: rawJson);

            var response = await mediator.Send(command, ct);

            // Redirect the browser to the Blazor result page
            return Results.Redirect(response.BrowserRedirectUrl);
        })
        // ⚠️ No authentication — this is called by the payment provider's server
        .WithName("PaymentGateway.Callback")
        .WithTags("PaymentGateway")
        .ExcludeFromDescription(); // Hide from Swagger (external-facing only)
    }
}
