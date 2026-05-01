using MediatR;

namespace Refahi.Modules.SupplyChain.Application.Contracts.Queries.AgreementProducts;

/// <summary>
/// Returns IDs of AgreementProducts that are fully displayable on a storefront:
/// CategoryId ∈ categoryIds AND Agreement.Status==Approved AND Agreement.ToDate>=now
/// AND Supplier.Status==Approved — all non-deleted.
/// Replaces the single-category <see cref="GetAgreementProductIdsByCategoryQuery"/> for multi-category subtree queries.
/// </summary>
public sealed record GetDisplayableAgreementProductIdsByCategoriesQuery(
    IReadOnlyList<int> CategoryIds)
    : IRequest<IReadOnlyList<Guid>>;
