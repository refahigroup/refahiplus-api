using MediatR;
using Refahi.Modules.Commerce.Application.Contracts;
using Refahi.Modules.Commerce.Domain;

namespace Refahi.Modules.Commerce.Application.Features.Operations.ResolveOperation;

public sealed class ResolveCommerceOperationCommandHandler(ICommerceRepository repository, ICommerceSecretProtector secrets, Refahi.Modules.Commerce.Application.Contracts.Providers.ICommerceMutationLock gate) :
    IRequestHandler<ResolveCommerceOperationCommand>
{
    public async Task<Unit> Handle(ResolveCommerceOperationCommand request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Evidence)) throw new CommerceDomainException("ثبت مستند بررسی الزامی است", "EVIDENCE_REQUIRED");
        var order = await repository.GetOrderByFulfillmentIdAsync(request.FulfillmentId, ct) ?? throw new CommerceDomainException("عملیات یافت نشد", "FULFILLMENT_NOT_FOUND");
        await using var held = await gate.AcquireAsync(order.Id, ct);
        order = await repository.GetFreshOrderAsync(order.Id, ct) ?? throw new InvalidOperationException();
        if (order.Status != CommerceOrderStatus.ManualReview)
            throw new CommerceDomainException("ثبت نتیجه فقط برای سفارش نیازمند بررسی مجاز است", "INVALID_RESOLUTION_STATE");
        var fulfillment = order.Fulfillments.Single(x => x.Id == request.FulfillmentId);
        var attempt = fulfillment.BeginAttempt("manual-resolution", $"manual:{fulfillment.Id:N}:{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}", secrets.Hash(request.Evidence));
        switch (request.Outcome.Trim().ToLowerInvariant())
        {
            case "fulfilled":
                if (string.IsNullOrWhiteSpace(request.ProviderOrderCode) || request.Tickets is null || request.Tickets.Count != order.Items.Where(x => x.ProviderKey == fulfillment.ProviderKey).Sum(x => x.Quantity)
                    || request.Tickets.Any(x => string.IsNullOrWhiteSpace(x.Code)) || request.Tickets.Select(x => x.Code).Distinct().Count() != request.Tickets.Count)
                    throw new CommerceDomainException("کد سفارش و بلیط‌ها الزامی است", "RESOLUTION_DATA_REQUIRED");
                fulfillment.Complete(request.ProviderOrderCode, request.Tickets.Select(x => (secrets.Hash(fulfillment.ProviderKey + ":" + x.Code), secrets.Protect(x.Code), x.IsChild))); attempt.Complete();
                if (order.Fulfillments.All(x => x.Status == ProviderFulfillmentStatus.Completed)) order.Complete();
                else order.ResumeFulfillment(); break;
            case "notcreated" when fulfillment.Tickets.Count == 0: fulfillment.Fail("عدم ایجاد سفارش توسط اپراتور تایید شد", false); attempt.Complete(); order.BeginCompensation(); break;
            case "cancelled" when fulfillment.ProviderKey != "touristpanel": fulfillment.MarkCancelled(); attempt.Complete(); order.BeginCompensation(); break;
            default: throw new CommerceDomainException("نتیجه بررسی معتبر نیست", "INVALID_RESOLUTION");
        }
        await repository.SaveChangesAsync(ct); return Unit.Value;
    }
}
