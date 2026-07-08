using MediatR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Refahi.Modules.Charge.Application.Contracts.Providers;
using Refahi.Modules.Charge.Domain.Aggregates;
using Refahi.Modules.Charge.Domain.Enums;
using Refahi.Modules.Charge.Domain.Repositories;
using Refahi.Modules.Orders.Application.Contracts.Commands;

namespace Refahi.Modules.Charge.Application.Services;

public sealed class ChargeFulfillmentProcessor
{
    private static readonly HashSet<int> AmbiguousCodes = [96, 100, 408, 501, 503];
    private readonly IChargeRequestRepository _requests; private readonly IChargeProviderResolver _providers;
    private readonly IChargeSecretProtector _protector; private readonly IMediator _mediator;
    private readonly IConfiguration _configuration; private readonly ILogger<ChargeFulfillmentProcessor> _logger;
    public ChargeFulfillmentProcessor(IChargeRequestRepository requests, IChargeProviderResolver providers,
        IChargeSecretProtector protector, IMediator mediator, IConfiguration configuration, ILogger<ChargeFulfillmentProcessor> logger)
    { _requests = requests; _providers = providers; _protector = protector; _mediator = mediator; _configuration = configuration; _logger = logger; }

    public async Task ProcessAsync(Guid requestId, CancellationToken ct)
    {
        var request = await _requests.GetAsync(requestId, ct) ?? throw new InvalidOperationException("درخواست شارژ یافت نشد");
        if (request.Status is ChargeRequestStatus.Fulfilled or ChargeRequestStatus.Refunded or ChargeRequestStatus.ManualReview) return;
        var previousStatus = request.Status; var now = DateTime.UtcNow;
        request.StartProcessing(Environment.MachineName, now, TimeSpan.FromMinutes(5)); await _requests.SaveChangesAsync(ct);
        try
        {
            if (previousStatus == ChargeRequestStatus.Paid) await PurchaseAsync(request, ct);
            else await TraceAsync(request, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Charge provider operation became ambiguous. ChargeRequestId={ChargeRequestId}, OrderId={OrderId}", request.Id, request.OrderId);
            request.MarkReconciliationPending(null, null, "نتیجه عملیات تامین‌کننده نامشخص است", DateTime.UtcNow.AddMinutes(1), DateTime.UtcNow);
            await _requests.SaveChangesAsync(ct);
        }
    }

    private async Task PurchaseAsync(ChargeRequest request, CancellationToken ct)
    {
        var result = await _providers.Get(request.ProviderName).PurchaseAsync(new(
            request.Operator, request.ServiceType, request.OriginMobileNumber, request.DestinationMobileNumber,
            request.ProviderCostMinor, request.CustomerInvoiceNumber, request.ProviderProductId,
            request.ProductCategory, request.PayBill,
            int.TryParse(_configuration["Charge:Providers:Eniac:ChannelId"], out var channelId) ? channelId : 102,
            _configuration["Charge:Providers:Eniac:ResellerName"], request.PinCategoryId, request.PinCount), ct);
        request.RecordAttempt(ChargeFulfillmentAttempt.Create(request.Id, FulfillmentAttemptType.Purchase, result.Success,
            result.EniacResultCode, result.OperatorResultCode, result.Rrn, result.OperatorTraceId, result.Message,
            result.RequestSnapshotJson, result.ResponseSnapshotJson, result.LatencyMilliseconds, DateTime.UtcNow));
        if (IsValidSuccess(request, result.Success, result.EniacResultCode, result.Rrn, result.Pins))
        {
            foreach (var pin in result.Pins) request.AddPin(_protector.Protect(pin.Serial), _protector.Protect(pin.Code), pin.AmountMinor);
            request.MarkFulfilled(result.Rrn, result.OperatorTraceId, result.EniacResultCode, result.OperatorResultCode, result.Message, DateTime.UtcNow);
            await _requests.SaveChangesAsync(ct); await CompleteOrderAsync(request, ct); return;
        }
        if (result.EniacResultCode == 0 || AmbiguousCodes.Contains(result.EniacResultCode))
        {
            request.MarkReconciliationPending(result.EniacResultCode, result.OperatorResultCode, result.Message, DateTime.UtcNow.AddMinutes(1), DateTime.UtcNow);
            await _requests.SaveChangesAsync(ct); return;
        }
        request.MarkFailed(result.EniacResultCode, result.OperatorResultCode, result.Message, DateTime.UtcNow);
        await _requests.SaveChangesAsync(ct); await RefundAsync(request, result.Message ?? "خرید شارژ توسط تامین‌کننده ناموفق بود", ct);
    }

    private async Task TraceAsync(ChargeRequest request, CancellationToken ct)
    {
        if (IsUnresolvedExpired(request)) { await ApplyUnresolvedPolicyAsync(request, ct); return; }
        var result = await _providers.Get(request.ProviderName).TraceAsync(new(0, request.CustomerInvoiceNumber,
            request.ProviderProductId, request.ProviderCostMinor, DateOnly.FromDateTime(request.CreatedAt)), ct);
        request.RecordAttempt(ChargeFulfillmentAttempt.Create(request.Id, FulfillmentAttemptType.Trace, result.Success,
            result.EniacResultCode, result.OperatorResultCode, result.Rrn, result.OperatorTraceId, result.Message,
            result.RequestSnapshotJson, result.ResponseSnapshotJson, result.LatencyMilliseconds, DateTime.UtcNow));
        if (result.Success && result.EniacResultCode == 0 && (result.PaymentResultCode is null or 0)
            && IsValidSuccess(request, true, 0, result.Rrn, result.Pins))
        {
            foreach (var pin in result.Pins) request.AddPin(_protector.Protect(pin.Serial), _protector.Protect(pin.Code), pin.AmountMinor);
            request.MarkFulfilled(result.Rrn, result.OperatorTraceId, result.EniacResultCode, result.OperatorResultCode, result.Message, DateTime.UtcNow);
            await _requests.SaveChangesAsync(ct); await CompleteOrderAsync(request, ct); return;
        }
        if (result.EniacResultCode == 0 || AmbiguousCodes.Contains(result.EniacResultCode) || result.EniacResultCode == 109)
        {
            var delay = TimeSpan.FromMinutes(Math.Min(30, Math.Pow(2, Math.Min(request.ReconciliationCount, 5))));
            request.MarkReconciliationPending(result.EniacResultCode, result.OperatorResultCode, result.Message, DateTime.UtcNow.Add(delay), DateTime.UtcNow);
            await _requests.SaveChangesAsync(ct); return;
        }
        request.MarkFailed(result.EniacResultCode, result.OperatorResultCode, result.Message, DateTime.UtcNow);
        await _requests.SaveChangesAsync(ct); await RefundAsync(request, result.Message ?? "تراکنش شارژ ناموفق بود", ct);
    }

    private bool IsUnresolvedExpired(ChargeRequest request)
    {
        var hours = int.TryParse(_configuration[$"Charge:Providers:{request.ProviderName}:UnresolvedTimeoutHours"], out var configuredHours)
            ? Math.Clamp(configuredHours, 1, 168) : 24;
        return request.PaidAt.HasValue && request.PaidAt.Value.AddHours(hours) <= DateTime.UtcNow;
    }

    private async Task ApplyUnresolvedPolicyAsync(ChargeRequest request, CancellationToken ct)
    {
        var action = _configuration[$"Charge:Providers:{request.ProviderName}:UnresolvedAction"] ?? "ManualReview";
        if (action.Equals("Refund", StringComparison.OrdinalIgnoreCase)) await RefundAsync(request, "مهلت تعیین وضعیت خرید شارژ به پایان رسید", ct);
        else { request.MarkManualReview("نیازمند بررسی دستی وضعیت تراکنش تامین‌کننده", DateTime.UtcNow); await _requests.SaveChangesAsync(ct); }
    }

    private async Task CompleteOrderAsync(ChargeRequest request, CancellationToken ct)
    {
        if (!request.OrderId.HasValue) return;
        await _mediator.Send(new UpdateOrderStatusCommand(request.OrderId.Value, OrderStatusInput.Processing), ct);
        await _mediator.Send(new UpdateOrderStatusCommand(request.OrderId.Value, OrderStatusInput.Delivered), ct);
    }

    private async Task RefundAsync(ChargeRequest request, string reason, CancellationToken ct)
    {
        if (!request.OrderId.HasValue) { request.MarkManualReview("سفارش برای بازگشت وجه یافت نشد", DateTime.UtcNow); await _requests.SaveChangesAsync(ct); return; }
        request.BeginRefund(DateTime.UtcNow); await _requests.SaveChangesAsync(ct);
        await _mediator.Send(new CancelOrderCommand(request.OrderId.Value, reason, $"charge-refund-{request.Id:N}"), ct);
        request.MarkRefunded(DateTime.UtcNow); await _requests.SaveChangesAsync(ct);
    }

    private static bool IsValidSuccess(ChargeRequest request, bool success, int code, string? rrn, IReadOnlyList<ProviderPinDto> pins)
        => success && code == 0 && !string.IsNullOrWhiteSpace(rrn)
            && (request.ServiceType != ChargeServiceType.PinCharge ||
                (pins.Count == request.PinCount && pins.All(x => !string.IsNullOrWhiteSpace(x.Serial) && !string.IsNullOrWhiteSpace(x.Code))));
}
