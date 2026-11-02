using MediatR;
using Refahi.Modules.Entertainments.Application.Contracts.Providers;
using Refahi.Modules.Entertainments.Domain;
namespace Refahi.Modules.Entertainments.Application.Features.Operations.ResolveOperation;

public sealed record RecheckEntertainmentOperationCommand(Guid FulfillmentId) : IRequest;
public sealed class RecheckEntertainmentOperationHandler(IEntertainmentRepository repository, IEntertainmentProviderFactory providers, IEntertainmentMutationLock gate)
    : IRequestHandler<RecheckEntertainmentOperationCommand>
{
    public async Task<Unit> Handle(RecheckEntertainmentOperationCommand request, CancellationToken ct)
    {
        var order = await repository.GetOrderByFulfillmentIdAsync(request.FulfillmentId, ct) ?? throw new EntertainmentDomainException("عملیات یافت نشد", "NOT_FOUND");
        await using var held = await gate.AcquireAsync(order.Id, ct);
        order = await repository.GetFreshOrderAsync(order.Id, ct) ?? throw new InvalidOperationException();
        var f = order.Fulfillments.Single(x => x.Id == request.FulfillmentId);
        if (providers.GetRequired(f.ProviderKey) is not IEntertainmentFulfillmentStatusProvider || !f.Attempts.Any(x => x.Operation == "fulfill")
            || order.Status != EntertainmentPurchaseStatus.ManualReview)
            throw new EntertainmentDomainException("استعلام مجدد برای این وضعیت مجاز نیست", "INVALID_RECHECK");
        f.RestartReconciliation();
        order.ResumeFulfillment();
        await repository.SaveChangesAsync(ct); return Unit.Value;
    }
}
