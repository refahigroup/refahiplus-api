using MediatR;
using Refahi.Modules.References.Application.Contracts.Queries;
using Refahi.Modules.SupplyChain.Application.Abstractions;
using Refahi.Modules.SupplyChain.Application.Contracts.Dtos;
using Refahi.Modules.SupplyChain.Application.Contracts.Queries.Agreements;

namespace Refahi.Modules.SupplyChain.Application.Features.Agreements.GetAgreementById;

public class GetAgreementByIdQueryHandler : IRequestHandler<GetAgreementByIdQuery, AgreementDto?>
{
    private readonly IAgreementRepository _repository;
    private readonly IMediator _mediator;

    public GetAgreementByIdQueryHandler(IAgreementRepository repository, IMediator mediator)
    {
        _repository = repository;
        _mediator = mediator;
    }

    public async Task<AgreementDto?> Handle(GetAgreementByIdQuery request, CancellationToken cancellationToken)
    {
        var agreement = await _repository.GetByIdAsync(request.Id, true, cancellationToken);

        if (agreement is null || agreement.IsDeleted)
            return null;

        var s = agreement.Supplier;
        var supplierName = s is not null
            ? (s.CompanyName ?? $"{s.FirstName} {s.LastName}".Trim())
            : string.Empty;

        // Batch-fetch category names for all products
        var categoryIds = agreement.Products
            .Where(p => !p.IsDeleted && p.CategoryId.HasValue)
            .Select(p => p.CategoryId!.Value)
            .Distinct()
            .ToList();

        var categoryNames = new Dictionary<int, string>();
        foreach (var catId in categoryIds)
        {
            var cat = await _mediator.Send(new GetCategoryByIdQuery(catId), cancellationToken);
            if (cat is not null) categoryNames[catId] = cat.Name;
        }

        var products = agreement.Products
            .Where(p => !p.IsDeleted)
            .Select(p => new AgreementProductDto(
                p.Id,
                p.AgreementId,
                p.Name,
                p.Description,
                p.CategoryId,
                p.CategoryId.HasValue && categoryNames.TryGetValue(p.CategoryId.Value, out var cn) ? cn : null,
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
