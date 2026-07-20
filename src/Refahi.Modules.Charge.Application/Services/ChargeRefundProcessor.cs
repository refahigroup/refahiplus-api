using MediatR;
using Microsoft.Extensions.Logging;
using Refahi.Modules.Charge.Domain.Aggregates;
using Refahi.Modules.Charge.Domain.Enums;
using Refahi.Modules.Charge.Domain.Repositories;
using Refahi.Modules.Orders.Application.Contracts.Commands;

namespace Refahi.Modules.Charge.Application.Services;

public sealed class ChargeRefundProcessor
{
    private static readonly TimeSpan LeaseDuration = TimeSpan.FromMinutes(5);
    private readonly IChargeRequestRepository _requests;
    private readonly ISender _sender;
    private readonly ILogger<ChargeRefundProcessor> _logger;

    public ChargeRefundProcessor(
        IChargeRequestRepository requests,
        ISender sender,
        ILogger<ChargeRefundProcessor> logger)
    {
        _requests = requests;
        _sender = sender;
        _logger = logger;
    }

    public async Task BeginAsync(
        ChargeRequest request,
        string reason,
        string idempotencyKey,
        CancellationToken ct)
    {
        if (!request.OrderId.HasValue || !request.PaymentId.HasValue)
        {
            request.MarkManualReview("پرداخت قابل بازگشت برای درخواست یافت نشد", DateTime.UtcNow);
            await _requests.SaveChangesAsync(ct);
            return;
        }

        request.BeginRefund(reason, idempotencyKey, DateTime.UtcNow);
        await _requests.SaveChangesAsync(ct);
        await ResumeAsync(request, ct);
    }

    public async Task ResumeAsync(ChargeRequest request, CancellationToken ct)
    {
        if (request.Status == ChargeRequestStatus.Refunded)
            return;

        if (request.Status != ChargeRequestStatus.Refunding)
            throw new InvalidOperationException("درخواست شارژ در وضعیت قابل بازیابی بازگشت وجه نیست");

        if (!request.OrderId.HasValue || !request.PaymentId.HasValue)
        {
            request.MarkManualReview("پرداخت قابل بازگشت برای درخواست یافت نشد", DateTime.UtcNow);
            await _requests.SaveChangesAsync(ct);
            return;
        }

        // Backward compatibility for requests that entered Refunding before the recovery columns existed.
        request.BeginRefund(
            request.RefundReason ?? "تکمیل بازگشت وجه درخواست شارژ",
            request.RefundIdempotencyKey ?? $"charge-refund-{request.Id:N}",
            DateTime.UtcNow);
        request.StartRefundAttempt(Environment.MachineName, DateTime.UtcNow, LeaseDuration);
        await _requests.SaveChangesAsync(ct);

        try
        {
            await _sender.Send(new CancelOrderCommand(
                request.OrderId.Value,
                request.RefundReason!,
                request.RefundIdempotencyKey!), ct);

            request.MarkRefunded(DateTime.UtcNow);
            await _requests.SaveChangesAsync(ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            var delay = TimeSpan.FromMinutes(Math.Min(30, Math.Pow(2, Math.Min(request.RefundAttemptCount, 5))));
            request.MarkRefundAttemptFailed(ex.Message, DateTime.UtcNow.Add(delay), DateTime.UtcNow);
            await _requests.SaveChangesAsync(CancellationToken.None);
            _logger.LogError(ex,
                "Charge refund attempt failed. ChargeRequestId={ChargeRequestId}, OrderId={OrderId}, Attempt={Attempt}",
                request.Id, request.OrderId, request.RefundAttemptCount);
            throw;
        }
    }
}
