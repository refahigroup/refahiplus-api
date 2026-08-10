using MediatR;
using Refahi.Modules.SupplyChain.Application.Contracts.Dtos;

namespace Refahi.Modules.SupplyChain.Application.Contracts.Queries.AgreementCategoryTerms;

public sealed record ResolveAgreementCategoryTermQuery(
    Guid SupplierId,
    int CategoryId,
    short SalesChannel,
    DateTimeOffset AtUtc
) : IRequest<ResolvedAgreementCategoryTermDto?>;

public sealed record AgreementCategoryTermResolutionRequest(
    Guid SupplierId,
    int CategoryId,
    short SalesChannel,
    DateTimeOffset AtUtc
);

public sealed record AgreementCategoryTermBatchResult(
    AgreementCategoryTermResolutionRequest Request,
    ResolvedAgreementCategoryTermDto? Term
);

public sealed record ResolveAgreementCategoryTermsBatchQuery(
    IReadOnlyList<AgreementCategoryTermResolutionRequest> Requests
) : IRequest<IReadOnlyList<AgreementCategoryTermBatchResult>>;
