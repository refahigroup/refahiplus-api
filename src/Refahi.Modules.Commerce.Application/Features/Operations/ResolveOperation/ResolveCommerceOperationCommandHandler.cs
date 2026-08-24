using MediatR;
using Refahi.Modules.Commerce.Application.Contracts;
using Refahi.Modules.Commerce.Domain;

namespace Refahi.Modules.Commerce.Application.Features.Operations.ResolveOperation;

public sealed class ResolveCommerceOperationCommandHandler(ICommerceRepository repository, ICommerceSecretProtector secrets) :
    IRequestHandler<ResolveCommerceOperationCommand>
{
    public async Task<Unit> Handle(ResolveCommerceOperationCommand request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Evidence)) throw new CommerceDomainException("ثبت مستند بررسی الزامی است", "EVIDENCE_REQUIRED");
        var order = await repository.GetOrderByFulfillmentIdAsync(request.FulfillmentId, ct) ?? throw new CommerceDomainException("عملیات یافت نشد", "FULFILLMENT_NOT_FOUND");
        var fulfillment = order.Fulfillments.Single(x => x.Id == request.FulfillmentId);
        var attempt = fulfillment.BeginAttempt("manual-resolution", $"manual:{fulfillment.Id:N}:{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}", secrets.Hash(request.Evidence));
        switch (request.Outcome.Trim().ToLowerInvariant())
        {
            case "fulfilled":
                if (string.IsNullOrWhiteSpace(request.ProviderOrderCode) || request.Tickets is null || request.Tickets.Count == 0)
                    throw new CommerceDomainException("کد سفارش و بلیط‌ها الزامی است", "RESOLUTION_DATA_REQUIRED");
                fulfillment.Complete(request.ProviderOrderCode, request.Tickets.Select(x => (secrets.Hash(x.Code), secrets.Protect(x.Code), x.IsChild))); attempt.Complete();
                if (order.Fulfillments.All(x => x.Status == ProviderFulfillmentStatus.Completed)) order.Complete();
                else order.ResumeFulfillment(); break;
            case "notcreated": fulfillment.Fail("عدم ایجاد سفارش توسط اپراتور تایید شد", false); attempt.Complete(); order.BeginCompensation(); break;
            case "cancelled": fulfillment.MarkCancelled(); attempt.Complete(); order.BeginCompensation(); break;
            default: throw new CommerceDomainException("نتیجه بررسی معتبر نیست", "INVALID_RESOLUTION");
        }
        await repository.SaveChangesAsync(ct); return Unit.Value;
    }
}
