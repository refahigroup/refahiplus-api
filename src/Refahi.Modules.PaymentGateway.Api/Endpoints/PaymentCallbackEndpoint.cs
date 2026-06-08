using MediatR;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Refahi.Modules.PaymentGateway.Application.Contracts.Features.ProcessCallback;
using Refahi.Modules.PaymentGateway.Domain.Enums;
using Refahi.Shared.Presentation;
using System.Globalization;
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

            var state = ReadFormValue(form, "State");
            if (string.IsNullOrWhiteSpace(state))
                state = ReadFormValue(form, "state");

            var status = ReadFormValue(form, "Status");
            var refNum = ReadFormValue(form, "RefNum");
            var resNum = ReadFormValue(form, "ResNum");
            var terminalId = ReadFormValue(form, "TerminalId");
            var mid = ReadFormValue(form, "MID");
            var traceNo = ReadFormValue(form, "TraceNo");
            var rrn = ReadFormValue(form, "Rrn");
            var amount = TryReadLong(form, "Amount", out var amountParseFailed);
            var wage = TryReadLong(form, "Wage");
            var affectiveAmount = TryReadLong(form, "AffectiveAmount");
            var securePan = ReadFormValue(form, "SecurePan");
            var hashedCardNumber = ReadFormValue(form, "HashedCardNumber");
            var token = ReadFormValue(form, "Token");

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
                RawCallbackJson: rawJson,
                Status: string.IsNullOrWhiteSpace(status) ? null : status,
                TerminalId: string.IsNullOrWhiteSpace(terminalId) ? null : terminalId,
                MID: string.IsNullOrWhiteSpace(mid) ? null : mid,
                Rrn: string.IsNullOrWhiteSpace(rrn) ? null : rrn,
                AmountMinor: amount,
                AmountParseFailed: amountParseFailed,
                WageMinor: wage,
                AffectiveAmountMinor: affectiveAmount,
                HashedCardNumber: string.IsNullOrWhiteSpace(hashedCardNumber) ? null : hashedCardNumber,
                Token: string.IsNullOrWhiteSpace(token) ? null : token);

            var response = await mediator.Send(command, ct);

            // Redirect the browser to the Blazor result page
            return Results.Redirect(response.BrowserRedirectUrl);
        })
        // ⚠️ No authentication — this is called by the payment provider's server
        .WithName("PaymentGateway.Callback")
        .WithTags("PaymentGateway")
        .ExcludeFromDescription(); // Hide from Swagger (external-facing only)
    }

    private static string ReadFormValue(IFormCollection form, string key)
    {
        if (form.TryGetValue(key, out var value))
            return value.ToString();

        var fallback = form.FirstOrDefault(item => item.Key.Equals(key, StringComparison.OrdinalIgnoreCase));
        return fallback.Value.ToString() ?? string.Empty;
    }

    private static long? TryReadLong(IFormCollection form, string key)
    {
        return TryReadLong(form, key, out _);
    }

    private static long? TryReadLong(IFormCollection form, string key, out bool parseFailed)
    {
        var value = ReadFormValue(form, key);
        if (string.IsNullOrWhiteSpace(value))
        {
            parseFailed = form.ContainsKey(key);
            return null;
        }

        if (long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var result))
        {
            parseFailed = false;
            return result;
        }

        parseFailed = true;
        return null;
    }
}
