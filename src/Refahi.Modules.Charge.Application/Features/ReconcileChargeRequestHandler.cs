using MediatR;
using Refahi.Modules.Charge.Application.Contracts.Features;
using Refahi.Modules.Charge.Application.Services;
using Refahi.Modules.Charge.Domain.Enums;
using Refahi.Modules.Charge.Domain.Repositories;

namespace Refahi.Modules.Charge.Application.Features;

public sealed class ReconcileChargeRequestHandler : IRequestHandler<ReconcileChargeRequestCommand, ReconcileChargeRequestResponse>
{
    private readonly IChargeRequestRepository _requests; 
    private readonly ChargeFulfillmentProcessor _processor;

    public ReconcileChargeRequestHandler(IChargeRequestRepository requests, ChargeFulfillmentProcessor processor)
    { 
        _requests = requests; 
        _processor = processor; 
    }

    public async Task<ReconcileChargeRequestResponse> Handle(ReconcileChargeRequestCommand c, CancellationToken ct)
    {
        var request = await _requests.GetAsync(c.RequestId, ct) ?? 
            throw new ArgumentException("درخواست شارژ یافت نشد");

        if (request.Status == ChargeRequestStatus.ManualReview && !c.ForceManualReviewReset)
            throw new ArgumentException("برای پردازش درخواست بررسی دستی باید گزینه اجباری فعال باشد");

        if (request.Status == ChargeRequestStatus.ManualReview)
            request.MarkReconciliationPending(
                request.EniacResultCode, 
                request.OperatorResultCode, 
                request.ProviderMessage, 
                DateTime.UtcNow, DateTime.UtcNow
            );

        await _requests.SaveChangesAsync(ct); 
        
        await _processor.ProcessAsync(request.Id, ct); 
        
        var updated = await _requests.GetAsync(request.Id, ct) ?? request;
        return new(updated.Id, updated.Status.ToString(), updated.Attempts.Count,
            updated.NextReconciliationAt, updated.ProviderRrn, updated.ProviderTraceId, updated.ProviderMessage);
    }
}
