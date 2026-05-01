using MediatR;
using Refahi.Modules.SupplyChain.Application.Abstractions;
using Refahi.Modules.SupplyChain.Application.Contracts.Dtos;
using Refahi.Modules.SupplyChain.Application.Contracts.Queries.Agreements;

namespace Refahi.Modules.SupplyChain.Application.Features.Agreements.GetAgreementById;

public class GetAgreementByIdQueryHandler : IRequestHandler<GetAgreementByIdQuery, AgreementDto?>
{
    private readonly IAgreementRepository _repository;

    public GetAgreementByIdQueryHandler(IAgreementRepository repository)
        => _repository = repository;

    public async Task<AgreementDto?> Handle(GetAgreementByIdQuery request, CancellationToken cancellationToken)
    {
        var agreement = await _repository.GetByIdAsync(request.Id, true, cancellationToken);

        if (agreement is null || agreement.IsDeleted)
            return null;

        var s = agreement.Supplier;
        var supplierName = s is not null
            ? (s.CompanyName ?? $"{s.FirstName} {s.LastName}".Trim())
            : string.Empty;

        var products = agreement.Products
            .Where(p => !p.IsDeleted)
            .Select(p => new AgreementProductDto(
                p.Id,
                p.AgreementId,
                p.Name,
                p.Description,
                p.CategoryId,
                (short)p.ProductType,
                (short)p.DeliveryType,
                (short)p.SalesModel,
                p.CommissionPercent,
                p.IsDeleted,
                p.CreatedAt))
            .ToList();

        return new AgreementDto(
            agreement.Id,
            agreement.AgreementNo,
            (short)agreement.AgreementType,
            agreement.AgreementType.ToString(),
            agreement.SupplierId,
            supplierName,
            agreement.FromDate,
            agreement.ToDate,
            (short)agreement.Status,
            agreement.Status.ToString(),
            agreement.StatusNote,
            agreement.IsDeleted,
            agreement.CreatedAt,
            agreement.UpdatedAt,
            products);
    }
}
