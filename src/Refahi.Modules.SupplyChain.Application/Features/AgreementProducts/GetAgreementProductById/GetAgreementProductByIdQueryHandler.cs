using MediatR;
using Refahi.Modules.SupplyChain.Application.Abstractions;
using Refahi.Modules.SupplyChain.Application.Contracts.Dtos;
using Refahi.Modules.SupplyChain.Application.Contracts.Queries.AgreementProducts;

namespace Refahi.Modules.SupplyChain.Application.Features.AgreementProducts.GetAgreementProductById;

public class GetAgreementProductByIdQueryHandler : IRequestHandler<GetAgreementProductByIdQuery, AgreementProductDto?>
{
    private readonly IAgreementRepository _repository;

    public GetAgreementProductByIdQueryHandler(IAgreementRepository repository)
        => _repository = repository;

    public async Task<AgreementProductDto?> Handle(GetAgreementProductByIdQuery request, CancellationToken cancellationToken)
    {
        var product = await _repository.GetProductByIdAsync(request.ProductId, cancellationToken);

        if (product is null || product.IsDeleted)
            return null;

        return new AgreementProductDto(
            product.Id,
            product.AgreementId,
            product.Name,
            product.Description,
            product.CategoryId,
            null,
            (short)product.ProductType,
            (short)product.DeliveryType,
            (short)product.SalesModel,
            product.CommissionPercent,
            product.IsDeleted,
            product.CreatedAt);
    }
}
