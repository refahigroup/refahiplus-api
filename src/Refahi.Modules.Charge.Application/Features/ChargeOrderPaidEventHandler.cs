using MediatR;
using Microsoft.Extensions.Logging;
using Refahi.Modules.Charge.Application.Contracts.Features;
using Refahi.Modules.Charge.Application.Services;
using Refahi.Modules.Charge.Domain.Enums;
using Refahi.Modules.Charge.Domain.Repositories;
using Refahi.Modules.Orders.Application.Contracts.IntegrationEvents;

namespace Refahi.Modules.Charge.Application.Features;

public sealed class ChargeOrderPaidEventHandler : INotificationHandler<OrderPaidIntegrationEvent>
{
    private readonly IChargeRequestRepository _requests; private readonly ILogger<ChargeOrderPaidEventHandler> _logger;
    public ChargeOrderPaidEventHandler(IChargeRequestRepository requests, ILogger<ChargeOrderPaidEventHandler> logger)
    { _requests = requests; _logger = logger; }
    public async Task Handle(OrderPaidIntegrationEvent e, CancellationToken ct)
    {
        if (!e.SourceModule.Equals("Charge", StringComparison.OrdinalIgnoreCase) ||
            !e.ReferenceType.Equals("ChargeRequest", StringComparison.OrdinalIgnoreCase)) return;
        var request = await _requests.GetAsync(e.SourceReferenceId, ct);
        if (request is null || request.UserId != e.UserId)
        { _logger.LogWarning("Charge paid event did not match request ownership. OrderId={OrderId}, ChargeRequestId={ChargeRequestId}", e.OrderId, e.SourceReferenceId); return; }
        request.MarkPaid(e.OrderId, e.PaymentId, DateTime.UtcNow);
        await _requests.SaveChangesAsync(ct);
        _logger.LogInformation("Charge request queued after order payment. ChargeRequestId={ChargeRequestId}, OrderId={OrderId}, SagaId={SagaId}", request.Id, e.OrderId, request.SagaId);
    }
}

public sealed class ReconcileChargeRequestHandler : IRequestHandler<ReconcileChargeRequestCommand>
{
    private readonly IChargeRequestRepository _requests; private readonly ChargeFulfillmentProcessor _processor;
    public ReconcileChargeRequestHandler(IChargeRequestRepository requests, ChargeFulfillmentProcessor processor)
    { _requests = requests; _processor = processor; }
    public async Task<Unit> Handle(ReconcileChargeRequestCommand c, CancellationToken ct)
    {
        var request = await _requests.GetAsync(c.RequestId, ct) ?? throw new ArgumentException("درخواست شارژ یافت نشد");
        if (request.Status == ChargeRequestStatus.ManualReview && !c.ForceManualReviewReset)
            throw new ArgumentException("برای پردازش درخواست بررسی دستی باید گزینه اجباری فعال باشد");
        if (request.Status == ChargeRequestStatus.ManualReview)
            request.MarkReconciliationPending(request.EniacResultCode, request.OperatorResultCode, request.ProviderMessage, DateTime.UtcNow, DateTime.UtcNow);
        await _requests.SaveChangesAsync(ct); await _processor.ProcessAsync(request.Id, ct); return Unit.Value;
    }
}
