using MediatR;
using Refahi.Modules.SupplyChain.Application.Contracts.Dtos;

namespace Refahi.Modules.SupplyChain.Application.Contracts.Queries.Agreements;

public sealed record GetAgreementsQuery(
    Guid? SupplierId = null,
    short? Status = null,
    short? Type = null,
    string? Search = null,
    int Page = 1,
    int Size = 20
) : IRequest<AgreementsPagedResponse>;

public sealed record AgreementsPagedResponse(
    IEnumerable<AgreementListItemDto> Data,
    int PageNumber,
    int PageSize,
    int TotalCount,
    int TotalPages);
