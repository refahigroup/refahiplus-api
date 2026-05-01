using MediatR;
using Refahi.Modules.SupplyChain.Application.Abstractions;
using Refahi.Modules.SupplyChain.Application.Contracts.Queries.AgreementProducts;

namespace Refahi.Modules.SupplyChain.Application.Features.AgreementProducts.GetDisplayableAgreementProductIdsByCategories;

public class GetDisplayableAgreementProductIdsByCategoriesQueryHandler
    : IRequestHandler<GetDisplayableAgreementProductIdsByCategoriesQuery, IReadOnlyList<Guid>>
{
    private readonly IAgreementRepository _repository;

    public GetDisplayableAgreementProductIdsByCategoriesQueryHandler(IAgreementRepository repository)
        => _repository = repository;

    public Task<IReadOnlyList<Guid>> Handle(
        GetDisplayableAgreementProductIdsByCategoriesQuery request, CancellationToken ct)
        => _repository.GetDisplayableProductIdsByCategoriesAsync(request.CategoryIds, ct);
}
