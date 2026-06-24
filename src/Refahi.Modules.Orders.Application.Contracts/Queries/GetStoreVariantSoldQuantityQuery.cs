using MediatR;

namespace Refahi.Modules.Orders.Application.Contracts.Queries;

public enum StoreVariantCapacityScope : short
{
    TotalPeriod = 1,
    PerEligibleDay = 2
}

public sealed record GetStoreVariantSoldQuantityQuery(
    Guid VariantId,
    DateOnly? UsageDate,
    StoreVariantCapacityScope CapacityScope,
    Guid? ExcludeOrderId = null
) : IRequest<int>;
