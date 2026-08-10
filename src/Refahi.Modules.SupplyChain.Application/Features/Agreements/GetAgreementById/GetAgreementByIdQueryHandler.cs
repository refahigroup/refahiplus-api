using MediatR;
using Refahi.Modules.References.Application.Contracts.Dtos;
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

    public async Task<AgreementDto?> Handle(
        GetAgreementByIdQuery request,
        CancellationToken cancellationToken
    )
    {
        var agreement = await _repository.GetByIdAsync(request.Id, true, cancellationToken);

        if (agreement is null || agreement.IsDeleted)
            return null;

        var s = agreement.Supplier;
        var supplierName = s is not null
            ? (s.CompanyName ?? $"{s.FirstName} {s.LastName}".Trim())
            : string.Empty;

        var categoryTree = await _mediator.Send(
            new GetCategoriesQuery(IncludeInactive: true),
            cancellationToken
        );
        var categoryNames = Flatten(categoryTree).ToDictionary(x => x.Id, x => x.Name);

        var products = agreement
            .Products.Where(p => !p.IsDeleted)
            .Select(p => new AgreementProductDto(
                p.Id,
                p.AgreementId,
                p.Name,
                p.Description,
                p.CategoryId,
                p.CategoryId.HasValue && categoryNames.TryGetValue(p.CategoryId.Value, out var cn)
                    ? cn
                    : null,
                (short)p.ProductType,
                (short)p.DeliveryType,
                (short)p.SalesModel,
                p.CommissionPercent,
                p.IsDeleted,
                p.CreatedAt,
                (short)p.PricingMode,
                p.VatApplicable
            ))
            .ToList();

        var terms = agreement
            .CategoryTerms.Where(x => !x.IsDeleted)
            .Select(x => new AgreementCategoryTermDto(
                x.Id,
                x.AgreementId,
                x.CategoryId,
                categoryNames.TryGetValue(x.CategoryId, out var categoryName) ? categoryName : null,
                (short)x.AllowedSalesChannels,
                x.CommissionPercent,
                x.IsDeleted,
                x.CreatedAt,
                x.UpdatedAt
            ))
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
            products,
            terms
        );
    }

    private static IEnumerable<CategoryDto> Flatten(IEnumerable<CategoryDto> categories)
    {
        foreach (var category in categories)
        {
            yield return category;
            if (category.Children is null)
                continue;
            foreach (var child in Flatten(category.Children))
                yield return child;
        }
    }
}
