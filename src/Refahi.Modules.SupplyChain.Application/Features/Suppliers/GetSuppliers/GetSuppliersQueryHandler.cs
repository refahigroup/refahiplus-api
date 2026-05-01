using MediatR;
using Refahi.Modules.SupplyChain.Application.Abstractions;
using Refahi.Modules.SupplyChain.Application.Contracts.Dtos;
using Refahi.Modules.SupplyChain.Application.Contracts.Queries.Suppliers;
using Refahi.Modules.SupplyChain.Domain.Aggregates;
using Refahi.Modules.SupplyChain.Domain.Enums;

namespace Refahi.Modules.SupplyChain.Application.Features.Suppliers.GetSuppliers;

public class GetSuppliersQueryHandler : IRequestHandler<GetSuppliersQuery, SuppliersPagedResponse>
{
    private readonly ISupplierRepository _repository;

    public GetSuppliersQueryHandler(ISupplierRepository repository)
        => _repository = repository;

    public async Task<SuppliersPagedResponse> Handle(GetSuppliersQuery request, CancellationToken cancellationToken)
    {
        SupplierStatus? status = request.Status.HasValue ? (SupplierStatus)request.Status.Value : null;
        SupplierType? type = request.Type.HasValue ? (SupplierType)request.Type.Value : null;

        int page = request.Page > 0 ? request.Page : 1;
        int size = request.Size > 0 ? request.Size : 20;

        var (items, total) = await _repository.GetPagedAsync(
            status, type, request.ProvinceId, request.Search, page, size, cancellationToken);

        int totalPages = (int)Math.Ceiling(total / (double)size);

        return new SuppliersPagedResponse(
            items.Select(MapToListItem),
            page,
            size,
            total,
            totalPages);
    }

    private static SupplierListItemDto MapToListItem(Supplier s)
    {
        var displayName = !string.IsNullOrWhiteSpace(s.CompanyName)
            ? s.CompanyName
            : $"{s.FirstName} {s.LastName}".Trim();

        return new SupplierListItemDto(
            s.Id,
            displayName,
            (short)s.Type,
            s.Type.ToString(),
            (short)s.Status,
            s.Status.ToString(),
            s.CityId,
            s.CreatedAt);
    }
}
