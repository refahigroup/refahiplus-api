using MediatR;
using Refahi.Modules.SupplyChain.Application.Abstractions;
using Refahi.Modules.SupplyChain.Application.Contracts.Queries.AgreementProducts;

namespace Refahi.Modules.SupplyChain.Application.Features.AgreementProducts.GetCommissionPercents;

public class GetCommissionPercentsByAgreementProductIdsQueryHandler
    : IRequestHandler<GetCommissionPercentsByAgreementProductIdsQuery, IReadOnlyDictionary<Guid, decimal>>
{
    private readonly IAgreementRepository _repository;

    public GetCommissionPercentsByAgreementProductIdsQueryHandler(IAgreementRepository repository)
        => _repository = repository;

    public Task<IReadOnlyDictionary<Guid, decimal>> Handle(
        GetCommissionPercentsByAgreementProductIdsQuery request, CancellationToken ct)
        => _repository.GetCommissionPercentsByIdsAsync(request.Ids, ct);
}
