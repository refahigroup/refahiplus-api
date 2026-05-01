using MediatR;

namespace Refahi.Modules.SupplyChain.Application.Contracts.Queries.AgreementProducts;

/// <summary>
/// Returns IDs of AgreementProducts whose parent Agreement is Approved and not expired,
/// and whose CategoryId matches <see cref="CategoryId"/>.
/// </summary>
public sealed record GetAgreementProductIdsByCategoryQuery(int CategoryId)
    : IRequest<IReadOnlyList<Guid>>;
