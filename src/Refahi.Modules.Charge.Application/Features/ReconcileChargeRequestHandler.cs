using MediatR;
using Refahi.Modules.Charge.Application.Contracts.Features;
using Refahi.Modules.Charge.Application.Services;
using Refahi.Modules.Charge.Domain.Enums;
using Refahi.Modules.Charge.Domain.Repositories;

namespace Refahi.Modules.Charge.Application.Features;

public sealed class ReconcileChargeRequestHandler : IRequestHandler<ReconcileChargeRequestCommand>
{
    private readonly IChargeRequestRepository _requests; 
    private readonly ChargeFulfillmentProcessor _processor;

    public ReconcileChargeRequestHandler(IChargeRequestRepository requests, ChargeFulfillmentProcessor processor)
    { 
        _requests = requests; 
        _processor = processor; 
    }

    public async Task<Unit> Handle(ReconcileChargeRequestCommand c, CancellationToken ct)
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
        
        return Unit.Value;
    }
}
