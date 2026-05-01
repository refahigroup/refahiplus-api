using MediatR;
using Refahi.Modules.SupplyChain.Application.Abstractions;
using Refahi.Modules.SupplyChain.Application.Contracts.Queries.AgreementProducts;

namespace Refahi.Modules.SupplyChain.Application.Features.AgreementProducts.GetAgreementProductIdsByCategory;

public class GetAgreementProductIdsByCategoryQueryHandler
    : IRequestHandler<GetAgreementProductIdsByCategoryQuery, IReadOnlyList<Guid>>
{
    private readonly IAgreementRepository _repository;

    public GetAgreementProductIdsByCategoryQueryHandler(IAgreementRepository repository)
        => _repository = repository;

    public Task<IReadOnlyList<Guid>> Handle(
        GetAgreementProductIdsByCategoryQuery request, CancellationToken ct)
        => _repository.GetApprovedProductIdsByCategoryAsync(request.CategoryId, ct);
}
