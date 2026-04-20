namespace Refahi.Modules.Store.Application.Contracts.Dtos.Products;

public sealed record VariantAttributeDto(
    Guid Id,
    string Name,
    int SortOrder,
    List<VariantAttributeValueDto> Values);

public sealed record VariantAttributeValueDto(
    Guid Id,
    string Value,
    int SortOrder);
