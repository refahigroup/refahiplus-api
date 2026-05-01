using MediatR;
using Refahi.Modules.SupplyChain.Application.Contracts.Dtos;

namespace Refahi.Modules.SupplyChain.Application.Contracts.Queries.Suppliers;

public sealed record GetSuppliersQuery(
    short? Status = null,
    short? Type = null,
    int? ProvinceId = null,
    string? Search = null,
    int Page = 1,
    int Size = 20
) : IRequest<SuppliersPagedResponse>;

public sealed record SuppliersPagedResponse(
    IEnumerable<SupplierListItemDto> Data,
    int PageNumber,
    int PageSize,
    int TotalCount,
    int TotalPages);
