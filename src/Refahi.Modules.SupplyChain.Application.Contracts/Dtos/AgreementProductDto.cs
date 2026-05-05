namespace Refahi.Modules.SupplyChain.Application.Contracts.Dtos;

public sealed record AgreementProductDto(
    Guid Id,
    Guid AgreementId,
    string Name,
    string? Description,
    int? CategoryId,
    string? CategoryName,
    short ProductType,
    short DeliveryType,
    short SalesModel,
    decimal CommissionPercent,
    bool IsDeleted,
    DateTimeOffset CreatedAt);
