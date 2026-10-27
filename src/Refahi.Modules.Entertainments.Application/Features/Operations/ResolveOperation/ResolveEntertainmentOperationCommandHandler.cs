using MediatR;
using Refahi.Modules.Entertainments.Application.Contracts;
using Refahi.Modules.Entertainments.Domain;

namespace Refahi.Modules.Entertainments.Application.Features.Operations.ResolveOperation;

public sealed class ResolveEntertainmentOperationCommandHandler(IEntertainmentRepository repository, IEntertainmentSecretProtector secrets, Refahi.Modules.Entertainments.Application.Contracts.Providers.IEntertainmentMutationLock gate) :
    IRequestHandler<ResolveEntertainmentOperationCommand>
{
    public async Task<Unit> Handle(ResolveEntertainmentOperationCommand request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Evidence)) throw new EntertainmentDomainException("ثبت مستند بررسی الزامی است", "EVIDENCE_REQUIRED");
        var order = await repository.GetOrderByFulfillmentIdAsync(request.FulfillmentId, ct) ?? throw new EntertainmentDomainException("عملیات یافت نشد", "FULFILLMENT_NOT_FOUND");
        await using var held = await gate.AcquireAsync(order.Id, ct);
        order = await repository.GetFreshOrderAsync(order.Id, ct) ?? throw new InvalidOperationException();
        var fulfillment = order.Fulfillments.Single(x => x.Id == request.FulfillmentId);
        var key = $"manual:{fulfillment.Id:N}:{request.IdempotencyKey}";
        var fingerprint = secrets.Hash(System.Text.Json.JsonSerializer.Serialize(new { request.Outcome, request.Evidence, request.ProviderOrderCode, request.Tickets, request.ActorId }));
        var prior = fulfillment.Attempts.SingleOrDefault(x => x.IdempotencyKey == key);
        if (prior is not null)
        {
            if (prior.PayloadHash != fingerprint) throw new EntertainmentDomainException("کلید یکتایی با نتیجه دیگری استفاده شده است", "IDEMPOTENCY_CONFLICT");
            if (prior.Outcome == ProviderOperationOutcome.Succeeded) return Unit.Value;
            throw new EntertainmentDomainException("ثبت نتیجه قبلی نیازمند بررسی است", "RESOLUTION_PENDING");
        }
        if (order.Status != EntertainmentPurchaseStatus.ManualReview)
            throw new EntertainmentDomainException("ثبت نتیجه فقط برای خرید نیازمند بررسی مجاز است", "INVALID_RESOLUTION_STATE");
        var attempt = fulfillment.BeginAttempt("manual-resolution", key, fingerprint);
        attempt.RecordEvidence(request.ActorId, secrets.Protect(request.Evidence));
        switch (request.Outcome.Trim().ToLowerInvariant())
        {
            case "fulfilled":
                if (string.IsNullOrWhiteSpace(request.ProviderOrderCode) || request.Tickets is null || request.Tickets.Count != order.Items.Where(x => x.ProviderKey == fulfillment.ProviderKey).Sum(x => x.Quantity)
                    || request.Tickets.Any(x => string.IsNullOrWhiteSpace(x.Code)) || request.Tickets.Select(x => x.Code).Distinct().Count() != request.Tickets.Count)
                    throw new EntertainmentDomainException("کد سفارش و بلیط‌ها الزامی است", "RESOLUTION_DATA_REQUIRED");
                fulfillment.Complete(request.ProviderOrderCode, request.Tickets.Select(x => (secrets.Hash(fulfillment.ProviderKey + ":" + x.Code), secrets.Protect(x.Code), x.IsChild))); attempt.Complete();
                if (order.Fulfillments.All(x => x.Status == ProviderFulfillmentStatus.Completed)) order.Complete();
                else order.ResumeFulfillment(); break;
            case "notcreated" when fulfillment.Tickets.Count == 0:
                foreach (var pending in fulfillment.Attempts.Where(x => x.Operation == "fulfill" && x.Outcome is ProviderOperationOutcome.Started or ProviderOperationOutcome.Ambiguous))
                    pending.Fail("عدم صدور با مستند مدیر تأیید شد", false);
                fulfillment.Fail("عدم ایجاد سفارش توسط اپراتور تایید شد", false);
                attempt.Complete(); order.BeginCompensation(); break;
            default: throw new EntertainmentDomainException("نتیجه بررسی معتبر نیست", "INVALID_RESOLUTION");
        }
        await repository.SaveChangesAsync(ct); return Unit.Value;
    }
}
