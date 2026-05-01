namespace Refahi.Modules.SupplyChain.Application.Contracts.Dtos;

public sealed record AgreementDto(
    Guid Id,
    string AgreementNo,
    short Type,
    string TypeName,
    Guid SupplierId,
    string SupplierName,
    DateTimeOffset FromDate,
    DateTimeOffset ToDate,
    short Status,
    string StatusName,
    string? StatusNote,
    bool IsDeleted,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    IReadOnlyList<AgreementProductDto> Products);
