using MediatR;

namespace Refahi.Modules.SupplyChain.Application.Contracts.Queries.AgreementProducts;

/// <summary>
/// Returns CommissionPercent keyed by AgreementProduct ID for the given list of IDs.
/// Used by the Store module to compute CommissionPrice without holding pricing fields on ShopProduct.
/// </summary>
public sealed record GetCommissionPercentsByAgreementProductIdsQuery(IReadOnlyList<Guid> Ids)
    : IRequest<IReadOnlyDictionary<Guid, decimal>>;
