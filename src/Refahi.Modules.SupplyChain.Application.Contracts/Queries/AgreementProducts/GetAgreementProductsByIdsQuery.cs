using MediatR;
using Refahi.Modules.SupplyChain.Application.Contracts.Dtos;

namespace Refahi.Modules.SupplyChain.Application.Contracts.Queries.AgreementProducts;

/// <summary>
/// Batch-fetches AgreementProducts by their IDs.
/// Returns a dictionary keyed by ID; missing IDs are absent from the result.
/// Used by the Store module to enrich product listings without N+1 queries.
/// </summary>
public sealed record GetAgreementProductsByIdsQuery(IReadOnlyList<Guid> Ids)
    : IRequest<IReadOnlyDictionary<Guid, AgreementProductDto>>;
