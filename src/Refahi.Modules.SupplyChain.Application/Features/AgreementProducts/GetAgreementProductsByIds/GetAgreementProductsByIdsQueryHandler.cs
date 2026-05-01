using MediatR;
using Refahi.Modules.SupplyChain.Application.Abstractions;
using Refahi.Modules.SupplyChain.Application.Contracts.Dtos;
using Refahi.Modules.SupplyChain.Application.Contracts.Queries.AgreementProducts;

namespace Refahi.Modules.SupplyChain.Application.Features.AgreementProducts.GetAgreementProductsByIds;

public class GetAgreementProductsByIdsQueryHandler
    : IRequestHandler<GetAgreementProductsByIdsQuery, IReadOnlyDictionary<Guid, AgreementProductDto>>
{
    private readonly IAgreementRepository _repository;

    public GetAgreementProductsByIdsQueryHandler(IAgreementRepository repository)
        => _repository = repository;

    public Task<IReadOnlyDictionary<Guid, AgreementProductDto>> Handle(
        GetAgreementProductsByIdsQuery request, CancellationToken ct)
        => _repository.GetProductsByIdsAsync(request.Ids, ct);
}
