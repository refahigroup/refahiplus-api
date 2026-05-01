namespace Refahi.Modules.SupplyChain.Application.Contracts.Dtos;

public sealed record AgreementListItemDto(
    Guid Id,
    string AgreementNo,
    short Type,
    string TypeName,
    Guid SupplierId,
    string SupplierName,
    short Status,
    string StatusName,
    DateTimeOffset FromDate,
    DateTimeOffset ToDate,
    DateTimeOffset CreatedAt);
