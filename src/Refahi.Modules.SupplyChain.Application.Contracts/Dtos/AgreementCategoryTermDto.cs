namespace Refahi.Modules.SupplyChain.Application.Contracts.Dtos;

public sealed record AgreementCategoryTermDto(
    Guid Id,
    Guid AgreementId,
    int CategoryId,
    string? CategoryName,
    short AllowedSalesChannels,
    decimal CommissionPercent,
    bool IsDeleted,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record ResolvedAgreementCategoryTermDto(
    Guid TermId,
    Guid AgreementId,
    Guid SupplierId,
    int MatchedCategoryId,
    int RequestedCategoryId,
    short AllowedSalesChannels,
    decimal CommissionPercent,
    DateTimeOffset ValidFromUtc,
    DateTimeOffset ValidToUtc);
