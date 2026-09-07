using MediatR;
using Refahi.Modules.Commerce.Application.Contracts.Providers;
using Refahi.Modules.Commerce.Domain;
namespace Refahi.Modules.Commerce.Application.Features.Operations.ResolveOperation;

public sealed record RecheckCommerceOperationCommand(Guid FulfillmentId) : IRequest;
public sealed class RecheckCommerceOperationHandler(ICommerceRepository repository, ICommerceProviderFactory providers, ICommerceMutationLock gate)
    : IRequestHandler<RecheckCommerceOperationCommand>
{
    public async Task<Unit> Handle(RecheckCommerceOperationCommand request, CancellationToken ct)
    {
        var order = await repository.GetOrderByFulfillmentIdAsync(request.FulfillmentId, ct) ?? throw new CommerceDomainException("عملیات یافت نشد", "NOT_FOUND");
        await using var held = await gate.AcquireAsync(order.Id, ct);
        order = await repository.GetFreshOrderAsync(order.Id, ct) ?? throw new InvalidOperationException();
        var f = order.Fulfillments.Single(x => x.Id == request.FulfillmentId);
        if (providers.GetRequired(f.ProviderKey) is not ICommerceFulfillmentStatusProvider || !f.Attempts.Any(x => x.Operation == "fulfill")
            || order.Status != CommerceOrderStatus.ManualReview)
            throw new CommerceDomainException("استعلام مجدد برای این وضعیت مجاز نیست", "INVALID_RECHECK");
        f.RestartReconciliation();
        order.ResumeFulfillment();
        await repository.SaveChangesAsync(ct); return Unit.Value;
    }
}
