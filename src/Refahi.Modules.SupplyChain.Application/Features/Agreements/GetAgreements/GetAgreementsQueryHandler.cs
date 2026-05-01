using MediatR;
using Refahi.Modules.SupplyChain.Application.Abstractions;
using Refahi.Modules.SupplyChain.Application.Contracts.Dtos;
using Refahi.Modules.SupplyChain.Application.Contracts.Queries.Agreements;
using Refahi.Modules.SupplyChain.Domain.Aggregates;
using Refahi.Modules.SupplyChain.Domain.Enums;

namespace Refahi.Modules.SupplyChain.Application.Features.Agreements.GetAgreements;

public class GetAgreementsQueryHandler : IRequestHandler<GetAgreementsQuery, AgreementsPagedResponse>
{
    private readonly IAgreementRepository _repository;

    public GetAgreementsQueryHandler(IAgreementRepository repository)
        => _repository = repository;

    public async Task<AgreementsPagedResponse> Handle(GetAgreementsQuery request, CancellationToken cancellationToken)
    {
        AgreementStatus? status = request.Status.HasValue ? (AgreementStatus)request.Status.Value : null;
        AgreementType? type = request.Type.HasValue ? (AgreementType)request.Type.Value : null;

        int page = request.Page > 0 ? request.Page : 1;
        int size = request.Size > 0 ? request.Size : 20;

        var (items, total) = await _repository.GetPagedAsync(
            request.SupplierId, status, type, request.Search, page, size, cancellationToken);

        int totalPages = (int)Math.Ceiling(total / (double)size);

        return new AgreementsPagedResponse(
            items.Select(MapToListItem),
            page,
            size,
            total,
            totalPages);
    }

    private static AgreementListItemDto MapToListItem(Agreement a)
    {
        var supplierName = a.Supplier is { } s
            ? (s.CompanyName ?? $"{s.FirstName} {s.LastName}".Trim())
            : string.Empty;

        return new AgreementListItemDto(
            a.Id,
            a.AgreementNo,
            (short)a.AgreementType,
            a.AgreementType.ToString(),
            a.SupplierId,
            supplierName,
            (short)a.Status,
            a.Status.ToString(),
            a.FromDate,
            a.ToDate,
            a.CreatedAt);
    }
}
